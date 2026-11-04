using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Queries;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
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
            [FromServices] VnPayPaymentService vnPayPaymentService) =>
        {
            var result = await sender.Send(new CreateOrderCommand(request.CustomerId, request.ReservationId));

            if (result.IsFailure)
                return Results.BadRequest(new { result.Error.Code, result.Error.Message });

            var order = result.Value;
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var paymentUrl = vnPayPaymentService.GeneratePaymentUrl(
                order.OrderId, order.TotalAmount, $"Ticketing order {order.OrderId}", ipAddress);

            return Results.Ok(new
            {
                OrderId = order.OrderId,
                ReservationId = order.ReservationId,
                TotalAmount = order.TotalAmount,
                Status = OrderStatus.Pending.ToString(),
                PaymentUrl = paymentUrl
            });
        })
        .WithName("CreateOrder");

        // FR-13: Customer order list
        group.MapGet("/customer/{customerId:guid}", async (
            [FromRoute] Guid customerId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerOrdersQuery(customerId));
            return result.Match<IResult>(
                orders => Results.Ok(orders),
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("GetCustomerOrders");

        // FR-13: Order detail
        group.MapGet("/{orderId:guid}", async (
            [FromRoute] Guid orderId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new GetOrderDetailQuery(orderId));
            return result.Match<IResult>(
                dto => Results.Ok(dto),
                error => error.Code == "NotFound"
                    ? Results.NotFound(new { error.Code, error.Message })
                    : Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("GetOrderDetail");

        // Customer refund request
        group.MapPost("/{orderId:guid}/refund", async (
            [FromRoute] Guid orderId,
            [FromBody] RequestRefundRequest request,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new RequestRefundCommand(orderId, request.Reason));
            return result.Match<IResult>(
                refundId => Results.Ok(new { RefundRequestId = refundId }),
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("RequestRefund");

        // Admin: list refund requests (with optional status filter)
        group.MapGet("/refunds", async (
            [FromQuery] string? status,
            [FromServices] IOrderRepository orderRepository) =>
        {
            RefundStatus? filter = Enum.TryParse<RefundStatus>(status, true, out var parsed) ? parsed : null;
            var refunds = await orderRepository.ListRefundRequestsAsync(filter);
            var dtos = refunds.Select(r => new RefundRequestDto(
                r.Id, r.OrderId, r.Reason, r.Status.ToString(), r.CreatedAt, r.ProcessedAt)).ToList();
            return Results.Ok(dtos);
        })
        .WithName("ListRefundRequests");

        // Admin: approve or reject a refund request
        group.MapPost("/refunds/{refundRequestId:guid}/process", async (
            [FromRoute] Guid refundRequestId,
            [FromBody] ProcessRefundRequest request,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new ProcessRefundCommand(refundRequestId, request.Approve, request.AdminNote));
            return result.Match<IResult>(
                refundId => Results.Ok(new { RefundRequestId = refundId }),
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("ProcessRefundRequest");

        // FR-22: Organizer attendee list for an event
        group.MapGet("/events/{eventId:guid}/attendees", async (
            [FromRoute] Guid eventId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new GetEventAttendeesQuery(eventId));
            return result.Match<IResult>(
                attendees => Results.Ok(attendees),
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("GetEventAttendees");
    }
}

public sealed record CreateOrderRequest(Guid CustomerId, Guid ReservationId);
public sealed record RequestRefundRequest(string Reason);
public sealed record ProcessRefundRequest(bool Approve, string? AdminNote = null);
public sealed record RefundRequestDto(Guid Id, Guid OrderId, string Reason, string Status, DateTime CreatedAt, DateTime? ProcessedAt);
