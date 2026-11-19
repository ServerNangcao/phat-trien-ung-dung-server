using System.Security.Claims;
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

internal sealed record CreateTicketTypeRequest(
    string Name,
    decimal Price,
    int TotalQuantity,
    int MaxPerOrder,
    DateTime? SaleStartsAt = null,
    DateTime? SaleEndsAt = null);

internal sealed record ReserveItemRequest(Guid TicketTypeId, int Quantity);

internal sealed record ReserveTicketsRequest(
    Guid EventId,
    Guid? TicketTypeId = null,
    int? Quantity = null,
    IReadOnlyList<ReserveItemRequest>? Items = null);

/// <summary>
/// Điểm vào duy nhất (public) của module Ticketing.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class TicketingModule
{
    public static IServiceCollection AddTicketingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<TicketingDbContext>(
            connectionStringName: "DefaultConnection",
            schema: TicketingDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(TicketingModule).Assembly);

        // Đăng ký QrSigner với khóa bí mật tối thiểu 32 ký tự
        var qrKey = configuration["Ticketing:QrKey"] ?? "development-ticketing-qr-signing-key-at-least-32b";
        services.AddSingleton(new QrSigner(qrKey));

        // Đăng ký nghiệp vụ Ticketing
        services.AddScoped<ITicketingModuleApi, TicketingService>();
        services.AddScoped<TicketingService>();

        // Đăng ký Background Worker tự động quét và hoàn trả vé giữ chỗ quá hạn 10 phút
        services.AddHostedService<ReservationExpiryWorker>();

        return services;
    }

    public static IEndpointRouteBuilder MapTicketingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        // 1. FR-10: Xem danh sách hạng vé theo sự kiện (Public)
        group.MapGet("/events/{eventId:guid}/ticket-types", async (
            Guid eventId,
            ITicketingModuleApi ticketingApi,
            CancellationToken ct) =>
        {
            var types = await ticketingApi.GetTypesAsync(eventId, ct);
            return Results.Ok(types);
        })
        .WithName("GetEventTicketTypes")
        .WithTags("Ticketing")
        .Produces<IReadOnlyList<TicketTypeDto>>(StatusCodes.Status200OK);

        // 2. FR-10: Tạo hạng vé mới (Organizer/Admin)
        group.MapPost("/events/{eventId:guid}/ticket-types", async (
            Guid eventId,
            CreateTicketTypeRequest request,
            TicketingDbContext db,
            CancellationToken ct) =>
        {
            var ticketType = TicketType.Create(
                eventId,
                request.Name,
                request.Price,
                request.TotalQuantity,
                request.MaxPerOrder,
                request.SaleStartsAt,
                request.SaleEndsAt);

            db.TicketTypes.Add(ticketType);

            // Bắn integration event cập nhật dải giá min/max sang module Events
            var existingPrices = await db.TicketTypes
                .Where(t => t.EventId == eventId && t.IsActive)
                .Select(t => t.Price)
                .ToListAsync(ct);

            existingPrices.Add(request.Price);

            db.AddOutboxMessage(new TicketTypesChangedIntegrationEvent
            {
                TargetEventId = eventId,
                MinPrice = existingPrices.Min(),
                MaxPrice = existingPrices.Max()
            });

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/events/{eventId}/ticket-types/{ticketType.Id}", new
            {
                ticketType.Id,
                ticketType.EventId,
                ticketType.Name,
                ticketType.Price,
                ticketType.TotalQuantity,
                ticketType.AvailableQuantity,
                ticketType.MaxPerOrder
            });
        })
        .WithName("CreateTicketType")
        .WithTags("Ticketing")
        .RequireAuthorization()
        .Produces(StatusCodes.Status201Created);

        // 3. FR-11: Giữ vé tạm thời 10 phút (Customer)
        Func<HttpContext, ReserveTicketsRequest, ITicketingModuleApi, CancellationToken, Task<IResult>> handleReservation = async (
            HttpContext context,
            ReserveTicketsRequest request,
            ITicketingModuleApi ticketingApi,
            CancellationToken ct) =>
        {
            var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                idempotencyKey = Guid.NewGuid().ToString();
            }

            var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub");

            if (!Guid.TryParse(subClaim, out var customerId))
            {
                customerId = Guid.NewGuid(); // Demo/Fallback mode
            }

            var firstItem = request.Items is { Count: > 0 } ? request.Items[0] : null;
            var targetTicketTypeId = request.TicketTypeId ?? firstItem?.TicketTypeId ?? Guid.Empty;
            var targetQuantity = request.Quantity ?? firstItem?.Quantity ?? 0;

            try
            {
                var reservation = await ticketingApi.ReserveAsync(
                    customerId,
                    request.EventId,
                    targetTicketTypeId,
                    targetQuantity,
                    idempotencyKey,
                    ct);

                var responseObj = new
                {
                    id = reservation.Id,
                    eventId = reservation.EventId,
                    ticketTypeId = reservation.TicketTypeId,
                    tierId = reservation.TicketTypeId,
                    customerId = reservation.CustomerId,
                    quantity = reservation.Quantity,
                    unitPrice = reservation.UnitPrice,
                    expiresAt = reservation.ExpiresAtUtc,
                    status = reservation.Status.ToString()
                };

                return Results.Created($"/api/reservations/{reservation.Id}", responseObj);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        };

        group.MapPost("/reservations", handleReservation)
            .WithName("ReserveTickets")
            .WithTags("Ticketing")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/ticketing/reservations", handleReservation)
            .WithName("ReserveTicketsAlias")
            .WithTags("Ticketing")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        // 4. Lấy thông tin lượt giữ vé
        group.MapGet("/reservations/{id:guid}", async (
            Guid id,
            ITicketingModuleApi ticketingApi,
            CancellationToken ct) =>
        {
            var reservation = await ticketingApi.GetReservationAsync(id, ct);
            return reservation is not null ? Results.Ok(reservation) : Results.NotFound();
        })
        .WithName("GetReservation")
        .WithTags("Ticketing")
        .Produces<ReservationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 5. Hủy lượt giữ vé
        group.MapDelete("/reservations/{id:guid}", async (
            HttpContext context,
            Guid id,
            ITicketingModuleApi ticketingApi,
            CancellationToken ct) =>
        {
            var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub");

            if (!Guid.TryParse(subClaim, out var customerId))
            {
                customerId = Guid.Empty;
            }

            await ticketingApi.ReleaseAsync(id, customerId, ct);
            return Results.NoContent();
        })
        .WithName("CancelReservation")
        .WithTags("Ticketing")
        .Produces(StatusCodes.Status204NoContent);

        // 6. FR-19: "Vé của tôi" - Lấy danh sách vé đã mua của khách hàng
        group.MapGet("/tickets/mine", async (
            HttpContext context,
            TicketingDbContext db,
            CancellationToken ct) =>
        {
            var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub");

            List<Ticket> tickets;
            if (Guid.TryParse(subClaim, out var customerId))
            {
                tickets = await db.Tickets
                    .AsNoTracking()
                    .Include(t => t.TicketType)
                    .Where(t => t.CustomerId == customerId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(ct);
            }
            else
            {
                tickets = await db.Tickets
                    .AsNoTracking()
                    .Include(t => t.TicketType)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(20)
                    .ToListAsync(ct);
            }

            var result = tickets.Select(t => new
            {
                id = t.Id,
                eventId = t.EventId,
                eventTitle = "Vé tham gia sự kiện",
                image = "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=900&q=80",
                venue = "TP. Hồ Chí Minh",
                date = t.CreatedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                tierName = t.TicketType != null ? t.TicketType.Name : "Vé tiêu chuẩn",
                holderName = string.IsNullOrEmpty(t.CustomerEmail) ? "Khách Hàng" : t.CustomerEmail,
                code = t.QrSignature,
                status = t.Status.ToLowerInvariant()
            });

            return Results.Ok(result);
        })
        .WithName("GetMyTickets")
        .WithTags("Ticketing")
        .AllowAnonymous();

        // 7. FR-17: Xuất ảnh QR Code định dạng PNG cho vé điện tử
        group.MapGet("/tickets/{id:guid}/qr", async (
            Guid id,
            TicketingDbContext db,
            CancellationToken ct) =>
        {
            var ticket = await db.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, ct);

            if (ticket is null)
            {
                return Results.NotFound();
            }

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrSignature, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var pngBytes = qrCode.GetGraphic(10);

            return Results.File(pngBytes, "image/png");
        })
        .WithName("GetTicketQrImage")
        .WithTags("Ticketing")
        .Produces(StatusCodes.Status200OK, contentType: "image/png")
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
