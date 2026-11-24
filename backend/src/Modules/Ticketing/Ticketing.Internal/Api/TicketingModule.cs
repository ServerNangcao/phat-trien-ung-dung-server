using System.Globalization;
using System.Security.Claims;
using Events.Contracts;
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
        var qrKey = configuration["Ticketing:QrKey"];
        var env = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? string.Empty;
        var isDevOrTest = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(env, "Testing", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(qrKey))
        {
            if (!isDevOrTest)
            {
                throw new InvalidOperationException("Ticketing:QrKey is required in production environment.");
            }
            qrKey = "development-ticketing-qr-signing-key-at-least-32b";
        }
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
            HttpContext context,
            Guid eventId,
            CreateTicketTypeRequest request,
            TicketingDbContext db,
            IEventsModuleApi eventsApi,
            CancellationToken ct) =>
        {
            var isAdmin = context.User.IsInRole("Admin");
            if (!context.User.IsInRole("Organizer") && !isAdmin)
            {
                return Results.Forbid();
            }

            if (!isAdmin)
            {
                var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                               context.User.FindFirstValue("sub") ??
                               context.User.FindFirstValue("id");

                if (!Guid.TryParse(subClaim, out var organizerId) || organizerId == Guid.Empty)
                {
                    return Results.Unauthorized();
                }

                var ev = await eventsApi.GetEventByIdAsync(eventId, ct);
                if (ev is null)
                {
                    return Results.NotFound(new { error = "Sự kiện không tồn tại." });
                }

                if (ev.OrganizerId != Guid.Empty && ev.OrganizerId != organizerId)
                {
                    return Results.Forbid();
                }
            }

            if (string.IsNullOrWhiteSpace(request.Name) ||
                request.Price < 0 ||
                request.TotalQuantity <= 0 ||
                request.MaxPerOrder <= 0 ||
                request.MaxPerOrder > request.TotalQuantity)
            {
                return Results.BadRequest(new { error = "Dữ liệu hạng vé không hợp lệ." });
            }

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
                capacity = ticketType.TotalQuantity,
                ticketType.TotalQuantity,
                quantityAvailable = ticketType.AvailableQuantity,
                ticketType.AvailableQuantity,
                ticketType.SoldQuantity,
                ticketType.ReservedQuantity,
                ticketType.MaxPerOrder
            });
        })
        .WithName("CreateTicketType")
        .WithTags("Ticketing")
        .RequireAuthorization()
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

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

            if (!Guid.TryParse(subClaim, out var customerId) || customerId == Guid.Empty)
            {
                return Results.Unauthorized();
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
            .RequireAuthorization()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/ticketing/reservations", handleReservation)
            .WithName("ReserveTicketsAlias")
            .WithTags("Ticketing")
            .RequireAuthorization()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status409Conflict);

        // 4. Lấy thông tin lượt giữ vé
        group.MapGet("/reservations/{id:guid}", async (
            HttpContext context,
            Guid id,
            ITicketingModuleApi ticketingApi,
            CancellationToken ct) =>
        {
            var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub");

            if (!Guid.TryParse(subClaim, out var customerId) || customerId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var reservation = await ticketingApi.GetReservationAsync(id, ct);
            if (reservation is null)
            {
                return Results.NotFound();
            }

            if (reservation.CustomerId != customerId && !context.User.IsInRole("Admin"))
            {
                return Results.Forbid();
            }

            return Results.Ok(reservation);
        })
        .WithName("GetReservation")
        .WithTags("Ticketing")
        .RequireAuthorization()
        .Produces<ReservationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
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

            if (!Guid.TryParse(subClaim, out var customerId) || customerId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            await ticketingApi.ReleaseAsync(id, customerId, ct);
            return Results.NoContent();
        })
        .WithName("CancelReservation")
        .WithTags("Ticketing")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        // 6. FR-19: "Vé của tôi" - Lấy danh sách vé đã mua của khách hàng
        group.MapGet("/tickets/mine", (HttpContext context, ITicketingModuleApi ticketingApi, IEventsModuleApi eventsApi, CancellationToken ct) =>
            GetMyTicketsAsync(context, ticketingApi, eventsApi, ct))
        .WithName("GetMyTickets")
        .WithTags("Ticketing")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 7. FR-17: Xuất ảnh QR Code định dạng PNG cho vé điện tử
        group.MapGet("/tickets/{id:guid}/qr", GetTicketQrImageAsync)
        .WithName("GetTicketQrImage")
        .WithTags("Ticketing")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK, contentType: "image/png")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    internal static async Task<IResult> GetMyTicketsAsync(
        HttpContext context,
        ITicketingModuleApi ticketingApi,
        IEventsModuleApi? eventsApi,
        CancellationToken ct)
    {
        var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                       context.User.FindFirstValue("sub");

        if (!Guid.TryParse(subClaim, out var customerId) || customerId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var tickets = await ticketingApi.GetMyTicketsAsync(customerId, ct);

        var eventIds = tickets.Select(t => t.EventId).Distinct();
        var eventsById = eventsApi is not null
            ? await eventsApi.GetEventsByIdsAsync(eventIds, ct)
            : new Dictionary<Guid, EventBriefDto>();

        var result = tickets.Select(t =>
        {
            eventsById.TryGetValue(t.EventId, out var ev);
            var eventDate = ev?.StartsAt ?? t.CreatedAt;

            return new
            {
                id = t.Id,
                orderId = t.OrderId,
                eventId = t.EventId,
                eventTitle = !string.IsNullOrWhiteSpace(ev?.Title) ? ev.Title : (!string.IsNullOrWhiteSpace(t.EventTitle) ? t.EventTitle : "Vé tham gia sự kiện"),
                image = !string.IsNullOrWhiteSpace(ev?.CoverImageUrl)
                    ? ev.CoverImageUrl
                    : "https://cdn.sukienbanve.id.vn/creative-morning.jpg",
                venue = FormatVenue(ev),
                date = eventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                tierName = !string.IsNullOrWhiteSpace(t.TicketTypeName) ? t.TicketTypeName : "Vé tiêu chuẩn",
                holderName = string.IsNullOrEmpty(t.CustomerEmail) ? "Khách Hàng" : t.CustomerEmail,
                code = t.QrPayload,
                status = t.Status.ToLowerInvariant()
            };
        });

        return Results.Ok(result);
    }

    internal static Task<IResult> GetMyTicketsAsync(
        HttpContext context,
        ITicketingModuleApi ticketingApi,
        CancellationToken ct) => GetMyTicketsAsync(context, ticketingApi, null, ct);

    internal static async Task<IResult> GetTicketQrImageAsync(
        HttpContext context,
        Guid id,
        TicketingDbContext db,
        CancellationToken ct)
    {
        var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                       context.User.FindFirstValue("sub");

        if (!Guid.TryParse(subClaim, out var userId) || userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var ticket = await db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.CustomerId != userId && !context.User.IsInRole("Admin"))
        {
            return Results.Forbid();
        }

        if (ticket.Status == TicketStatus.Cancelled)
        {
            return Results.Conflict(new { error = "Vé đã bị hủy, không thể xuất mã QR." });
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(ticket.QrSignature, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var pngBytes = qrCode.GetGraphic(10);

        return Results.File(pngBytes, "image/png");
    }

    private static string FormatVenue(EventBriefDto? ev)
    {
        if (ev is null)
        {
            return "TP. Hồ Chí Minh";
        }

        var hasName = !string.IsNullOrWhiteSpace(ev.VenueName);
        var hasCity = !string.IsNullOrWhiteSpace(ev.VenueCity);

        if (hasName && hasCity)
        {
            return ev.VenueName.Contains(ev.VenueCity, StringComparison.OrdinalIgnoreCase)
                ? ev.VenueName
                : $"{ev.VenueName}, {ev.VenueCity}";
        }

        if (hasName)
        {
            return ev.VenueName;
        }

        return hasCity ? ev.VenueCity : "TP. Hồ Chí Minh";
    }
}
