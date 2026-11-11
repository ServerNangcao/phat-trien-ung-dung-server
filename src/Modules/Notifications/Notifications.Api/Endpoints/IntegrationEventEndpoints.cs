using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

namespace Ticketing.Modules.Notifications.Api.Endpoints;

public static class IntegrationEventEndpoints
{
    public static void MapIntegrationEventEndpoints(this IEndpointRouteBuilder app)
    {
        // Inbound integration endpoints: the outbox workers of other modules deliver
        // events here. The Inbox pattern dedupes by EventId, and the inbox row commits
        // atomically with the Pending email history row, so a crash after the 202 ack is
        // recovered by the retry worker instead of losing the email. The SMTP send itself
        // runs in a background scope so the caller is never blocked on a mail server.
        app.MapPost("/api/integration-events/ticket-issued", async (
            [FromBody] TicketIssuedIntegrationEvent @event,
            HttpContext context,
            [FromServices] TicketEmailProcessor processor,
            [FromServices] IServiceScopeFactory scopeFactory,
            [FromServices] ILogger<Program> logger) =>
        {
            if (@event.EventId == Guid.Empty)
                return Results.BadRequest(new { error = "EventId is required for inbox dedupe." });

            var prepared = await processor.PrepareIncomingAsync(@event, context.RequestAborted);

            if (!prepared.IsNew)
                return Results.Ok(new { Duplicated = true, EventId = @event.EventId });

            if (prepared.Email is not null)
                StartBackgroundDelivery(scopeFactory, logger, prepared.Email.Id, @event.EventId);

            return Results.Json(new { EventId = @event.EventId }, statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("TicketIssuedInbound");

        // ADR-0003: payment arrived after the reservation expired. The customer gets the
        // apology email with the automatic refund request confirmation (UC-ORD-02 step 6).
        app.MapPost("/api/integration-events/order-paid-after-expiry", async (
            [FromBody] OrderPaidAfterExpiryIntegrationEvent @event,
            HttpContext context,
            [FromServices] RefundEmailProcessor processor,
            [FromServices] IServiceScopeFactory scopeFactory,
            [FromServices] ILogger<Program> logger) =>
        {
            if (@event.EventId == Guid.Empty)
                return Results.BadRequest(new { error = "EventId is required for inbox dedupe." });

            var notice = new RefundNotice(
                RefundEmailKind.LatePaymentAutoRefund, @event.EventId, @event.OrderId,
                @event.CustomerEmail, @event.TotalAmount, @event.Reason, @event.PaidAt);

            return await PrepareAndDispatchAsync(notice, processor, scopeFactory, logger, context.RequestAborted);
        })
        .WithName("OrderPaidAfterExpiryInbound");

        // A refund request was approved: the customer is told the money is on its way back.
        app.MapPost("/api/integration-events/refund-processed", async (
            [FromBody] RefundProcessedIntegrationEvent @event,
            HttpContext context,
            [FromServices] RefundEmailProcessor processor,
            [FromServices] IServiceScopeFactory scopeFactory,
            [FromServices] ILogger<Program> logger) =>
        {
            if (@event.EventId == Guid.Empty)
                return Results.BadRequest(new { error = "EventId is required for inbox dedupe." });

            var notice = new RefundNotice(
                RefundEmailKind.RefundProcessed, @event.EventId, @event.OrderId,
                @event.CustomerEmail, @event.TotalAmount, string.Empty, @event.RefundedAt);

            return await PrepareAndDispatchAsync(notice, processor, scopeFactory, logger, context.RequestAborted);
        })
        .WithName("RefundProcessedInbound");
    }

    private static async Task<IResult> PrepareAndDispatchAsync(
        RefundNotice notice,
        RefundEmailProcessor processor,
        IServiceScopeFactory scopeFactory,
        ILogger<Program> logger,
        CancellationToken requestAborted)
    {
        var prepared = await processor.PrepareAsync(notice, requestAborted);

        if (!prepared.IsNew)
            return Results.Ok(new { Duplicated = true, EventId = notice.EventId });

        if (prepared.Email is not null)
            StartBackgroundDelivery(scopeFactory, logger, prepared.Email.Id, notice.EventId);

        return Results.Json(new { EventId = notice.EventId }, statusCode: StatusCodes.Status202Accepted);
    }

    private static void StartBackgroundDelivery(
        IServiceScopeFactory scopeFactory, ILogger logger, Guid emailId, Guid eventId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var emailRepository = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IEmailDispatcher>();
                var email = await emailRepository.GetByIdAsync(emailId, CancellationToken.None);
                if (email is null)
                {
                    logger.LogError("Prepared email {EmailId} disappeared before delivery", emailId);
                    return;
                }

                await dispatcher.DeliverAsync(email, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background delivery failed for event {EventId}", eventId);
            }
        }, CancellationToken.None);
    }
}
