using System.Text;
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
        // An optional discount code (FR-16) is validated server-side and reduces the total.
        group.MapPost("/", async (
            [FromBody] CreateOrderRequest request,
            HttpContext context,
            [FromServices] ISender sender,
            [FromServices] VnPayPaymentService vnPayPaymentService) =>
        {
            var result = await sender.Send(new CreateOrderCommand(request.CustomerId, request.ReservationId, request.DiscountCode));

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
                DiscountAmount = order.DiscountAmount,
                Status = OrderStatus.Pending.ToString(),
                PaymentUrl = paymentUrl
            });
        })
        .WithName("CreateOrder");

        // FR-16: create a discount code (Organizer/Admin; TV1's host applies the policy).
        group.MapPost("/discounts", async (
            [FromBody] CreateDiscountCodeRequest request,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new CreateDiscountCodeCommand(
                request.Code, request.Type, request.Value, request.MaxRedemptions, request.ExpiresAtUtc));

            return result.Match<IResult>(
                dto => Results.Ok(dto),
                error => error.Code == "Conflict"
                    ? Results.Conflict(new { error.Code, error.Message })
                    : Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("CreateDiscountCode");

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

        // FR-16: customer cancels an unpaid order; the reservation is released best-effort.
        group.MapPost("/{orderId:guid}/cancel", async (
            [FromRoute] Guid orderId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new CancelOrderCommand(orderId));
            return result.Match<IResult>(
                cancelled => Results.Ok(new { OrderId = cancelled.OrderId, Status = cancelled.Status }),
                error => error.Code == "NotFound"
                    ? Results.NotFound(new { error.Code, error.Message })
                    : Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("CancelOrder");

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
                r.Id, r.OrderId, r.Reason, r.Status.ToString(), r.CreatedAt, r.ProcessedAt, r.ProcessedNote)).ToList();
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

        // FR-23: revenue summary for one event (same data the Events dashboard reads
        // through IOrdersModuleApi.GetSalesSummaryAsync).
        group.MapGet("/events/{eventId:guid}/sales-summary", async (
            [FromRoute] Guid eventId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new GetEventSalesSummaryQuery(eventId));
            return result.Match<IResult>(
                summary => Results.Ok(summary),
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("GetEventSalesSummary");

        // FR-22: attendee list exported as a file (RFC 4180 CSV with UTF-8 BOM so Excel
        // opens it correctly).
        group.MapGet("/events/{eventId:guid}/attendees/export", async (
            [FromRoute] Guid eventId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new GetEventAttendeesQuery(eventId));
            return result.Match<IResult>(
                attendees =>
                {
                    var csv = CsvBuilder.Build(
                        new[] { "TicketId", "OrderId", "CustomerId", "TicketTypeId", "TicketType", "Status", "IssuedAtUtc" },
                        attendees.Select(a => new[]
                        {
                            a.TicketId.ToString(), a.OrderId.ToString(), a.CustomerId.ToString(),
                            a.TicketTypeId.ToString(), a.TicketType, a.Status, a.IssuedAt.ToString("O")
                        }));
                    return CsvFile(csv, $"attendees-{eventId:N}.csv");
                },
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("ExportEventAttendees");

        // FR-22: order list for the event exported as a file.
        group.MapGet("/events/{eventId:guid}/orders/export", async (
            [FromRoute] Guid eventId,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new GetEventOrdersQuery(eventId));
            return result.Match<IResult>(
                orders =>
                {
                    var csv = CsvBuilder.Build(
                        new[] { "OrderId", "CustomerId", "ReservationId", "TotalAmount", "Status", "CreatedAt", "Items" },
                        orders.Select(o => new[]
                        {
                            o.OrderId.ToString(), o.CustomerId.ToString(), o.ReservationId.ToString(),
                            o.TotalAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                            o.Status, o.CreatedAt.ToString("O"),
                            string.Join("; ", o.Items.Select(i => $"{i.TicketTypeName} x{i.Quantity}"))
                        }));
                    return CsvFile(csv, $"orders-{eventId:N}.csv");
                },
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("ExportEventOrders");
    }

    private static IResult CsvFile(string csv, string fileName)
    {
        return Results.File(
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(),
            contentType: "text/csv; charset=utf-8",
            fileDownloadName: fileName);
    }
}

/// <summary>RFC 4180 CSV rows: fields with quotes, commas or newlines are double-quoted and escaped.</summary>
internal static class CsvBuilder
{
    public static string Build(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(EscapeRow(header));
        foreach (var row in rows)
            sb.AppendLine(EscapeRow(row));
        return sb.ToString();
    }

    private static string EscapeRow(IReadOnlyList<string> cells) =>
        string.Join(",", cells.Select(EscapeCell));

    private static string EscapeCell(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

public sealed record CreateOrderRequest(Guid CustomerId, Guid ReservationId, string? DiscountCode = null);
public sealed record CreateDiscountCodeRequest(string Code, string Type, decimal Value, int? MaxRedemptions = null, DateTime? ExpiresAtUtc = null);
public sealed record RequestRefundRequest(string Reason);
public sealed record ProcessRefundRequest(bool Approve, string? AdminNote = null);
public sealed record RefundRequestDto(Guid Id, Guid OrderId, string Reason, string Status, DateTime CreatedAt, DateTime? ProcessedAt, string? ProcessedNote = null);
