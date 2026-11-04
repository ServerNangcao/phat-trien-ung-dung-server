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
        // Inbound integration endpoint: the outbox workers of other modules deliver events
        // here. The Inbox pattern dedupes by EventId, and the inbox row commits atomically
        // with the Pending email history row, so a crash after the 202 ack is recovered by
        // the retry worker instead of losing the email. The SMTP send itself runs in a
        // background scope so the caller is never blocked on a mail server.
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
            {
                var emailId = prepared.Email.Id;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var emailRepository = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
                        var scopedProcessor = scope.ServiceProvider.GetRequiredService<TicketEmailProcessor>();
                        var email = await emailRepository.GetByIdAsync(emailId, CancellationToken.None);
                        if (email is null)
                        {
                            logger.LogError("Prepared email {EmailId} disappeared before delivery", emailId);
                            return;
                        }

                        await scopedProcessor.DeliverAsync(email, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background delivery failed for TicketIssued event {EventId}", @event.EventId);
                    }
                }, CancellationToken.None);
            }

            return Results.Json(new { EventId = @event.EventId }, statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("TicketIssuedInbound");
    }
}
