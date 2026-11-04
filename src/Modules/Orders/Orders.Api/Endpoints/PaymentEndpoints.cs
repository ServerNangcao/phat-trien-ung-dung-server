using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Infrastructure.Payment;

namespace Ticketing.Modules.Orders.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments");

        // VNPay IPN callback (server-to-server, no browser redirect).
        // VNPay retries this endpoint until it receives a 200, so every path below either
        // commits state idempotently or acks without changing state.
        group.MapPost("/vnpay/ipn", async (
            HttpContext context,
            [FromServices] VnPayPaymentService vnPayService,
            [FromServices] ISender sender) =>
        {
            var query = context.Request.Query
                .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());

            var callback = vnPayService.ParseCallback(query);
            var isValid = vnPayService.VerifyCallbackSignature(query, callback.SecureHash);

            if (!isValid)
            {
                return Results.BadRequest(new { RspCode = "99", Message = "Invalid signature" });
            }

            // ResponseCode other than "00" means the payment failed at the gateway;
            // ack receipt so VNPay stops retrying and leave the order Pending for a retry.
            if (callback.ResponseCode != "00")
            {
                return Results.Ok(new { RspCode = "00", Message = "Payment not successful; no state change" });
            }

            var result = await sender.Send(new HandleVnPayIpnCommand(callback.TransactionId, callback.Amount));

            if (result.IsFailure)
            {
                return Results.BadRequest(new { RspCode = "99", Message = result.Error.Message });
            }

            return Results.Ok(new { RspCode = "00", Message = "Success", OrderId = result.Value.OrderId });
        })
        .WithName("VnPayIpn");

        // VNPay return URL (browser redirect after payment)
        group.MapGet("/vnpay/return", async (
            HttpContext context,
            [FromServices] VnPayPaymentService vnPayService,
            [FromServices] IOrderRepository orderRepository) =>
        {
            var query = context.Request.Query
                .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());

            var callback = vnPayService.ParseCallback(query);
            var isValid = vnPayService.VerifyCallbackSignature(query, callback.SecureHash);

            var order = await orderRepository.GetByIdAsync(callback.TransactionId);

            var redirectUrl = isValid && order is not null
                ? $"/payment/result?orderId={callback.TransactionId}&status={order.Status}"
                : $"/payment/result?orderId={callback.TransactionId}&status=invalid";

            return Results.Redirect(redirectUrl);
        })
        .WithName("VnPayReturn");
    }
}
