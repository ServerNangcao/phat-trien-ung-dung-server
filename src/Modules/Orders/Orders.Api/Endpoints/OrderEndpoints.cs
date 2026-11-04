using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Infrastructure.Payment;

namespace Ticketing.Modules.Orders.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");

        // Creates an order from an active reservation and returns the VNPay redirect URL.
        // Prices and quantities are read from the Ticketing reservation, never from the client.
        group.MapPost("/", async (
            [FromBody] CreateOrderRequest request,
            HttpContext context,
            [FromServices] ISender sender,
            [FromServices] IOrderRepository orderRepository,
            [FromServices] VnPayPaymentService vnPayPaymentService) =>
        {
            var result = await sender.Send(new CreateOrderCommand(request.CustomerId, request.ReservationId));

            if (result.IsFailure)
                return Results.BadRequest(new { result.Error.Code, result.Error.Message });

            var order = await orderRepository.GetByIdAsync(result.Value);
            if (order is null)
                return Results.BadRequest(new { Code = "OrderNotFound", Message = "Created order could not be loaded." });

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var paymentUrl = vnPayPaymentService.GeneratePaymentUrl(
                order.Id, order.TotalAmount, $"Ticketing order {order.Id}", ipAddress);

            return Results.Ok(new
            {
                OrderId = order.Id,
                ReservationId = order.ReservationId,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                PaymentUrl = paymentUrl
            });
        })
        .WithName("CreateOrder");
    }
}

public sealed record CreateOrderRequest(Guid CustomerId, Guid ReservationId);
