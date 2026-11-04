using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure.Payment;
using Ticketing.Modules.Orders.Contracts;

namespace Ticketing.Modules.Orders.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments");

        // VNPay IPN callback (server-to-server, no browser redirect)
        group.MapPost("/vnpay/ipn", async (
            HttpContext context,
            [FromServices] VnPayPaymentService vnPayService,
            [FromServices] IOrderRepository orderRepository,
            [FromServices] ILogger logger) =>
        {
            var query = context.Request.Query
                .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());

            var callback = vnPayService.ParseCallback(query);
            var isValid = vnPayService.VerifyCallbackSignature(query, callback.SecureHash);

            if (!isValid)
            {
                logger.LogWarning("VNPay IPN signature verification failed for order {OrderId}", callback.TransactionId);
                return Results.BadRequest(new { RspCode = "99", Message = "Invalid signature" });
            }

            var order = await orderRepository.GetByIdAsync(callback.TransactionId);
            if (order is null)
            {
                logger.LogWarning("VNPay IPN referenced unknown order {OrderId}", callback.TransactionId);
                return Results.BadRequest(new { RspCode = "99", Message = "Order not found" });
            }

            // Idempotency: if already paid, respond success without re-processing
            if (order.Status == OrderStatus.Paid)
            {
                return Results.Ok(new { RspCode = "00", Message = "Already confirmed" });
            }

            logger.LogInformation("VNPay IPN confirmed payment for order {OrderId}, status {Status}", callback.TransactionId, callback.TransactionStatus);
            return Results.Ok(new { RspCode = "00", Message = "Success" });
        })
        .WithName("VnPayIpn")
        .WithOpenApi();

        // VNPay return URL (browser redirect after payment)
        group.MapGet("/vnpay/return", async (
            HttpContext context,
            [FromServices] VnPayPaymentService vnPayService) =>
        {
            var query = context.Request.Query
                .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());

            var callback = vnPayService.ParseCallback(query);
            var isValid = vnPayService.VerifyCallbackSignature(query, callback.SecureHash);

            var redirectUrl = isValid
                ? $"/payment/result?orderId={callback.TransactionId}&status={callback.TransactionStatus}"
                : $"/payment/result?orderId={callback.TransactionId}&status=invalid";

            return Results.Redirect(redirectUrl);
        })
        .WithName("VnPayReturn")
        .WithOpenApi();
    }
}