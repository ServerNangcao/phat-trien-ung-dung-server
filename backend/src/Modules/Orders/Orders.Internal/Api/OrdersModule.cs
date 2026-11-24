using System.Security.Claims;
using Events.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts;
using Orders.Internal.Domain;
using Orders.Internal.Infrastructure;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core;

namespace Orders.Internal.Api;

internal sealed record CheckoutOrderRequest(
    Guid ReservationId,
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    Guid? CustomerId = null);

/// <summary>
/// Điểm vào duy nhất (public) của module Orders.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersDbContext>(
            connectionStringName: "DefaultConnection",
            schema: OrdersDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(OrdersModule).Assembly);

        services.AddScoped<IOrdersModuleApi, OrdersModuleApi>();

        return services;
    }

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapPost("/", async (
            HttpContext context,
            CheckoutOrderRequest request,
            OrdersDbContext db,
            ITicketingModuleApi ticketingApi,
            IEventsModuleApi eventsApi,
            CancellationToken ct) =>
        {
            var existingOrder = await db.Orders.FirstOrDefaultAsync(o => o.ReservationId == request.ReservationId, ct);
            if (existingOrder is not null)
            {
                return Results.Ok(new
                {
                    orderId = existingOrder.Id,
                    reservationId = existingOrder.ReservationId,
                    totalAmount = existingOrder.TotalAmount,
                    status = existingOrder.Status,
                    paymentUrl = $"/payment/return?status=success&order={existingOrder.Id}"
                });
            }

            var reservation = await ticketingApi.GetReservationAsync(request.ReservationId, ct);
            if (reservation is null)
            {
                return Results.BadRequest(new { error = "Không tìm thấy lượt giữ vé hoặc đã hết hạn." });
            }

            var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub");

            var customerId = request.CustomerId ??
                             (Guid.TryParse(subClaim, out var parsedSub) ? parsedSub : reservation.CustomerId);

            if (customerId == Guid.Empty)
            {
                customerId = Guid.NewGuid();
            }

            var totalAmount = reservation.Quantity * reservation.UnitPrice;
            var customerName = string.IsNullOrWhiteSpace(request.Name) ? "Khách Hàng" : request.Name;
            var customerEmail = string.IsNullOrWhiteSpace(request.Email) ? "customer@ticketing.local" : request.Email;

            var eventInfo = await eventsApi.GetEventByIdAsync(reservation.EventId, ct);
            var eventTitle = !string.IsNullOrWhiteSpace(eventInfo?.Title) ? eventInfo.Title : "Sự kiện";

            var ticketTypes = await ticketingApi.GetTypesAsync(reservation.EventId, ct);
            var ticketTypeName = ticketTypes.FirstOrDefault(t => t.Id == reservation.TicketTypeId)?.Name ?? "Vé tiêu chuẩn";

            // 1. Tạo đơn hàng Orders
            var order = Order.Create(
                customerId,
                reservation.EventId,
                reservation.Id,
                customerName,
                customerEmail,
                totalAmount);

            order.AddItem(reservation.TicketTypeId, ticketTypeName, reservation.UnitPrice, reservation.Quantity);
            order.MarkPaid();
            db.Orders.Add(order);

            // 2. Tạo thanh toán Payment thành công
            var payment = Payment.Create(order.Id, "MockPay", Guid.NewGuid().ToString("N"), totalAmount);
            payment.MarkSucceeded("MOCK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), "{}");
            db.Payments.Add(payment);

            await db.SaveChangesAsync(ct);

            // 3. Phát hành vé điện tử có QR code ký số HMAC
            await ticketingApi.IssueTicketsForOrderAsync(
                order.Id,
                reservation.EventId,
                customerId,
                customerEmail,
                customerName,
                eventTitle,
                reservation.TicketTypeId,
                ticketTypeName,
                reservation.UnitPrice,
                reservation.Quantity,
                ct);

            return Results.Ok(new
            {
                orderId = order.Id,
                reservationId = order.ReservationId,
                totalAmount = order.TotalAmount,
                status = "Paid",
                paymentUrl = $"/payment/return?status=success&order={order.Id}"
            });
        })
        .WithName("CreateAndPayOrder")
        .WithTags("Orders")
        .AllowAnonymous();

        // GET /api/orders/organizer — Danh sách đơn hàng & trạng thái check-in thuộc các sự kiện của Organizer (FR-22)
        group.MapGet("/organizer", async (
            HttpContext context,
            OrdersDbContext db,
            IEventsModuleApi eventsApi,
            ITicketingModuleApi ticketingApi,
            Guid? eventId,
            string? status,
            string? search,
            CancellationToken ct) =>
        {
            var isAdmin = context.User.IsInRole("Admin");
            var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub") ??
                           context.User.FindFirstValue("id");

            if (!Guid.TryParse(subClaim, out var currentUserId) || currentUserId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            Dictionary<Guid, EventBriefDto> eventsById;
            IQueryable<Order> ordersQuery = db.Orders
                .AsNoTracking()
                .Include(o => o.Items);

            if (!isAdmin)
            {
                var organizerEvents = await eventsApi.GetEventsByOrganizerAsync(currentUserId, ct);
                if (organizerEvents.Count == 0)
                {
                    return Results.Ok(Array.Empty<object>());
                }

                eventsById = organizerEvents.ToDictionary(e => e.Id);
                var ownedEventIds = eventsById.Keys.ToList();

                if (eventId.HasValue)
                {
                    if (!eventsById.ContainsKey(eventId.Value))
                    {
                        return Results.Forbid();
                    }
                    ordersQuery = ordersQuery.Where(o => o.EventId == eventId.Value);
                }
                else
                {
                    ordersQuery = ordersQuery.Where(o => ownedEventIds.Contains(o.EventId));
                }
            }
            else
            {
                eventsById = new Dictionary<Guid, EventBriefDto>();
                if (eventId.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.EventId == eventId.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                ordersQuery = ordersQuery.Where(o => o.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var escapedSearch = search.Trim()
                    .Replace(@"\", @"\\", StringComparison.Ordinal)
                    .Replace("%", @"\%", StringComparison.Ordinal)
                    .Replace("_", @"\_", StringComparison.Ordinal);
                ordersQuery = ordersQuery.Where(o =>
                    EF.Functions.ILike(o.CustomerName, $"%{escapedSearch}%") ||
                    EF.Functions.ILike(o.CustomerEmail, $"%{escapedSearch}%"));
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.CreatedAt)
                .Take(200)
                .ToListAsync(ct);

            if (orders.Count == 0)
            {
                return Results.Ok(Array.Empty<object>());
            }

            if (isAdmin)
            {
                var eventIds = orders.Select(o => o.EventId).Distinct();
                var fetchedEvents = await eventsApi.GetEventsByIdsAsync(eventIds, ct);
                foreach (var kv in fetchedEvents)
                {
                    eventsById[kv.Key] = kv.Value;
                }
            }

            var orderIds = orders.Select(o => o.Id).ToList();
            var tickets = await ticketingApi.GetTicketsByOrderIdsAsync(orderIds, ct);
            var checkedInOrderIds = tickets
                .Where(t => string.Equals(t.Status, "Used", StringComparison.OrdinalIgnoreCase) || t.UsedAt.HasValue)
                .Select(t => t.OrderId)
                .ToHashSet();

            var response = orders.Select(o =>
            {
                eventsById.TryGetValue(o.EventId, out var ev);
                var firstItem = o.Items.FirstOrDefault();
                var totalQty = o.Items.Sum(i => i.Quantity);
                var tierLabel = o.Items.Count <= 1
                    ? (firstItem?.TicketTypeName ?? "Vé tiêu chuẩn")
                    : string.Join(", ", o.Items.Select(i => i.TicketTypeName).Distinct());

                var paymentStatus = o.Status switch
                {
                    OrderStatus.Paid => "Paid",
                    OrderStatus.Refunded => "Refunded",
                    OrderStatus.PaidAfterExpiry => "Refunded",
                    _ => "Pending"
                };

                return new
                {
                    id = o.Id,
                    orderCode = $"ORD-{o.Id.ToString("N")[..8].ToUpperInvariant()}",
                    eventId = o.EventId,
                    eventTitle = ev?.Title ?? "Sự kiện",
                    buyerName = o.CustomerName,
                    buyerEmail = o.CustomerEmail,
                    tierName = tierLabel,
                    quantity = totalQty > 0 ? totalQty : 1,
                    totalAmount = o.TotalAmount,
                    paymentStatus,
                    rawStatus = o.Status,
                    checkInStatus = checkedInOrderIds.Contains(o.Id) ? "CheckedIn" : "NotCheckedIn",
                    createdAt = o.CreatedAt.ToString("O")
                };
            });

            return Results.Ok(response);
        })
        .WithName("GetOrganizerOrders")
        .WithTags("Orders")
        .RequireAuthorization(p => p.RequireRole("Organizer", "Admin"));

        return app;
    }
}
