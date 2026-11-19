using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Internal.Domain;
using Orders.Internal.Infrastructure;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core;

namespace Orders.Internal.Api;

public sealed record CheckoutOrderRequest(
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

            // 1. Tạo đơn hàng Orders
            var order = Order.Create(
                customerId,
                reservation.EventId,
                reservation.Id,
                customerName,
                customerEmail,
                totalAmount);

            order.AddItem(reservation.TicketTypeId, "Vé điện tử", reservation.UnitPrice, reservation.Quantity);
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
                "Sự kiện",
                reservation.TicketTypeId,
                "Vé tiêu chuẩn",
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

        return app;
    }
}
