using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core;
using Ticketing.Internal.Application;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module Ticketing.
/// Đăng ký DI và map endpoints (FR-10, FR-11, FR-12, FR-17, FR-19).
/// </summary>
public static class TicketingModule
{
    public static IServiceCollection AddTicketingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Đăng ký DbContext với schema "ticketing" riêng biệt
        services.AddModuleDbContext<TicketingDbContext>(
            connectionStringName: "DefaultConnection",
            schema: TicketingDbContext.SchemaName,
            configuration: configuration);

        // 2. Ký số QR HMAC-SHA256
        var qrKey = configuration["Ticketing:QrKey"] ?? "development-ticketing-qr-signing-key-32-bytes";
        services.AddSingleton(new QrSigner(qrKey));

        // 3. Service nghiệp vụ
        services.AddScoped<ITicketingModuleApi, TicketingService>();
        services.AddScoped<TicketingService>();

        // 4. Background worker tự động nhả vé quá hạn 10 phút
        services.AddHostedService<ReservationExpiryWorker>();

        // 5. Quét validators
        services.AddModuleValidators(typeof(TicketingModule).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapTicketingEndpoints(this IEndpointRouteBuilder app)
    {
        // FR-10: Xem danh sách hạng vé
        app.MapGet("/api/events/{eventId:guid}/ticket-types", async (
            Guid eventId,
            ITicketingModuleApi api,
            CancellationToken ct) =>
        {
            var types = await api.GetTypesAsync(eventId, ct);
            return Results.Ok(types);
        });

        // FR-10: Tạo hạng vé mới
        app.MapPost("/api/events/{eventId:guid}/ticket-types", async (
            Guid eventId,
            CreateTicketTypeRequest request,
            TicketingDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0 || request.Capacity <= 0 || request.MaxPerOrder <= 0)
            {
                return Results.BadRequest(new { message = "Thông tin hạng vé không hợp lệ." });
            }

            var type = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Name = request.Name.Trim(),
                Price = request.Price,
                Capacity = request.Capacity,
                QuantityAvailable = request.Capacity,
                MaxPerOrder = request.MaxPerOrder,
                Version = 0,
                CreatedAt = DateTime.UtcNow
            };

            db.TicketTypes.Add(type);

            // Bắn event cập nhật min/max price sang module Events
            var prices = await db.TicketTypes
                .Where(x => x.EventId == eventId)
                .Select(x => x.Price)
                .ToListAsync(ct);
            prices.Add(request.Price);

            db.AddOutboxMessage(new TicketTypesChangedIntegrationEvent
            {
                TargetEventId = eventId,
                MinPrice = prices.Min(),
                MaxPrice = prices.Max()
            });

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/events/{eventId}/ticket-types", new TicketTypeDto(
                type.Id,
                type.EventId,
                type.Name,
                type.Price,
                type.Capacity,
                type.QuantityAvailable,
                type.MaxPerOrder));
        });

        // FR-11: Giữ vé tạm thời 10 phút (kèm Idempotency-Key)
        app.MapPost("/api/reservations", async (
            ReserveRequest request,
            HttpContext httpContext,
            ITicketingModuleApi api,
            CancellationToken ct) =>
        {
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(new { message = "Idempotency-Key header là bắt buộc." });
            }

            var customerIdStr = httpContext.User.FindFirst("sub")?.Value;
            var customerId = Guid.TryParse(customerIdStr, out var id) ? id : Guid.Empty;

            try
            {
                var result = await api.ReserveAsync(
                    customerId,
                    request.EventId,
                    request.TicketTypeId,
                    request.Quantity,
                    idempotencyKey,
                    ct);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        });

        // FR-11: Tra cứu lượt giữ vé
        app.MapGet("/api/reservations/{id:guid}", async (
            Guid id,
            ITicketingModuleApi api,
            CancellationToken ct) =>
        {
            var item = await api.GetReservationAsync(id, ct);
            return item is not null ? Results.Ok(item) : Results.NotFound();
        });

        // Hủy lượt giữ vé
        app.MapDelete("/api/reservations/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            ITicketingModuleApi api,
            CancellationToken ct) =>
        {
            var customerIdStr = httpContext.User.FindFirst("sub")?.Value;
            var customerId = Guid.TryParse(customerIdStr, out var uId) ? uId : Guid.Empty;

            await api.ReleaseAsync(id, customerId, ct);
            return Results.NoContent();
        });

        // FR-19: Vé của tôi
        app.MapGet("/api/tickets/mine", async (
            HttpContext httpContext,
            ITicketingModuleApi api,
            CancellationToken ct) =>
        {
            var customerIdStr = httpContext.User.FindFirst("sub")?.Value;
            var customerId = Guid.TryParse(customerIdStr, out var uId) ? uId : Guid.Empty;

            var tickets = await api.GetMyTicketsAsync(customerId, ct);
            return Results.Ok(tickets);
        });

        // FR-17, FR-19: Render mã QR vé dạng ảnh PNG
        app.MapGet("/api/tickets/{id:guid}/qr", async (
            Guid id,
            TicketingDbContext db,
            CancellationToken ct) =>
        {
            var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (ticket is null)
            {
                return Results.NotFound();
            }

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(ticket.QrPayload, QRCodeGenerator.ECCLevel.Q);
            using var png = new PngByteQRCode(data);

            return Results.File(png.GetGraphic(8), "image/png");
        });

        return app;
    }

    public sealed record CreateTicketTypeRequest(string Name, decimal Price, int Capacity, int MaxPerOrder);
    public sealed record ReserveRequest(Guid EventId, Guid TicketTypeId, int Quantity);
}
