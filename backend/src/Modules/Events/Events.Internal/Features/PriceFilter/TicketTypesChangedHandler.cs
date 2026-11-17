using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core.Outbox;

namespace Events.Internal.Features.PriceFilter;

/// <summary>
/// Handler nhan TicketTypesChangedIntegrationEvent tu Outbox cua Ticketing module.
/// Cap nhat MinPrice / MaxPrice vao Event de ho tro filter gia hieu qua.
/// </summary>
internal sealed class TicketTypesChangedHandler(EventsDbContext db)
    : IIntegrationEventHandler<TicketTypesChangedIntegrationEvent>
{
    public async Task HandleAsync(
        TicketTypesChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // Inbox deduplication — tranh xu ly lap
        var isNew = await db.TryAddInboxMessageAsync(
            integrationEvent.EventId,
            nameof(TicketTypesChangedHandler),
            cancellationToken);

        if (!isNew) return;

        var @event = await db.Events
            .FirstOrDefaultAsync(e => e.Id == integrationEvent.TargetEventId, cancellationToken);

        if (@event is null)
        {
            // Event chua ton tai trong Events module — bo qua (eventual consistency)
            return;
        }

        @event.UpdatePriceRange(integrationEvent.MinPrice, integrationEvent.MaxPrice);
        await db.SaveChangesAsync(cancellationToken);
    }
}
