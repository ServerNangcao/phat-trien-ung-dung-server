using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure.Payment;

namespace Ticketing.Modules.Orders.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments");

        // VNPay IPN callback (server-to-server, no browser redirect).
        // Responses follow the VNPay contract: HTTP 200 with a RspCode. 00 confirms
        // processing (and stops VNPay's retries); a specific code (01/04/97/99) reports
        // a permanent condition without leaving VNPay retrying forever.
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
                return Results.Ok(new { RspCode = "97", Message = "Invalid signature" });
            }

            // Both gates must be success: ResponseCode is the gateway result and
            // TransactionStatus the transaction state (e.g. suspected fraud stays non-00).
            if (callback.ResponseCode != "00" || callback.TransactionStatus != "00")
            {
                return Results.Ok(new { RspCode = "00", Message = "Payment not successful; no state change" });
            }

            var result = await sender.Send(new HandleVnPayIpnCommand(callback.TransactionId, callback.Amount));

            if (result.IsFailure)
            {
                var rspCode = result.Error.Code switch
                {
                    "NotFound" => "01",        // order not found
                    "ValidationError" => "04", // invalid amount / state
                    _ => "99"
                };
                return Results.Ok(new { RspCode = rspCode, Message = result.Error.Message });
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

        // FR-16: MoMo e-wallet payment. Creates the hosted payment URL for a pending order.
        group.MapPost("/momo/create", async (
            [FromBody] MoMoCreateRequest request,
            [FromServices] MoMoPaymentService momoService,
            [FromServices] IOrderRepository orderRepository) =>
        {
            var order = await orderRepository.GetByIdAsync(request.OrderId);
            if (order is null)
                return Results.NotFound(new { error = $"Order {request.OrderId} not found." });

            if (order.Status != OrderStatus.Pending)
                return Results.BadRequest(new { error = $"Order {order.Id} is {order.Status}; only pending orders can be paid." });

            var result = await momoService.CreatePaymentAsync(
                order.Id, order.TotalAmount, $"Ticketing order {order.Id}");

            return result.Match<IResult>(
                payment => Results.Ok(new { OrderId = payment.OrderId, RequestId = payment.RequestId,
                    PayUrl = payment.PayUrl, QrCodeUrl = payment.QrCodeUrl }),
                error => Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status503ServiceUnavailable));
        })
        .WithName("CreateMoMoPayment");

        // FR-16: MoMo IPN callback. MoMo retries while the merchant responds non-2xx, so:
        // bad signature → 400 (retry, tampering), success → 200 {resultCode: 0}, permanent
        // state errors (amount mismatch) → 200 {resultCode: 4} (retrying cannot fix it),
        // transient/unresolvable (missing order, Ticketing outage) → 503 (retry).
        group.MapPost("/momo/ipn", async (
            [FromBody] Dictionary<string, string?> ipn,
            [FromServices] MoMoPaymentService momoService,
            [FromServices] ISender sender) =>
        {
            ipn.TryGetValue("signature", out var signature);
            if (!momoService.VerifyIpnSignature(ipn, signature ?? string.Empty))
                return Results.BadRequest(new { resultCode = 97, message = "Invalid signature" });

            ipn.TryGetValue("resultCode", out var resultCodeRaw);
            if (resultCodeRaw != "0")
            {
                // Payment failed or was cancelled at the gateway: no state change, but the
                // IPN is acknowledged so MoMo stops retrying.
                return Results.Ok(new { resultCode = 0, message = "Payment not successful; no state change" });
            }

            ipn.TryGetValue("orderId", out var orderIdRaw);
            ipn.TryGetValue("amount", out var amountRaw);
            if (!Guid.TryParse(orderIdRaw, out var orderId)
                || !decimal.TryParse(amountRaw, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                return Results.Ok(new { resultCode = 4, message = "Malformed orderId or amount; not retryable" });
            }

            var result = await sender.Send(new CompleteOrderPaymentCommand(orderId, amount));

            return result.Match<IResult>(
                success => Results.Ok(new { resultCode = 0, message = "Success", OrderId = success.OrderId }),
                error => error.Code == "ValidationError"
                    ? Results.Ok(new { resultCode = 4, message = error.Message })
                    : Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status503ServiceUnavailable));
        })
        .WithName("MoMoIpn");
    }
}

public sealed record MoMoCreateRequest(Guid OrderId);
