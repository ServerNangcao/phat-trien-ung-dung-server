using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

namespace Ticketing.Modules.Notifications.Api.Endpoints;

public static class IntegrationEventEndpoints
{
    public static void MapIntegrationEventEndpoints(this IEndpointRouteBuilder app)
    {
        // Inbound integration endpoint: the outbox workers of other modules deliver events
        // here. The Inbox pattern dedupes by EventId so a redelivered TicketIssued event
        // never triggers a second email. The email send runs in a background scope so the
        // caller is never blocked on SMTP; a crash between ack and send is recovered by
        // the retry worker (Pending emails older than 30s are re-attempted).
        app.MapPost("/api/integration-events/ticket-issued", async (
            [FromBody] TicketIssuedIntegrationEvent @event,
            HttpContext context,
            [FromServices] IInbox inbox,
            [FromServices] IServiceScopeFactory scopeFactory,
            [FromServices] ILogger<Program> logger) =>
        {
            var eventId = @event.EventId == Guid.Empty ? Guid.NewGuid() : @event.EventId;
            @event = @event with { EventId = eventId };

            var payload = JsonSerializer.Serialize(@event);
            var isNew = await inbox.TryRecordAsync(eventId, "TicketIssued", payload);

            if (!isNew)
                return Results.Ok(new { Duplicated = true, EventId = eventId });

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    await publisher.Publish(@event);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background processing failed for TicketIssued event {EventId}", eventId);
                }
            }, context.RequestAborted);

            return Results.Json(new { EventId = eventId }, statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("TicketIssuedInbound");
    }
}
