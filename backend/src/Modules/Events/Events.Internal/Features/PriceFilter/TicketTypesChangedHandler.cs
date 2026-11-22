using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core.Outbox;

namespace Events.Internal.Features.PriceFilter;

/// <summary>
/// Handler nhận TicketTypesChangedIntegrationEvent từ Outbox của Ticketing module.
/// Cập nhật MinPrice / MaxPrice vào Event để hỗ trợ filter giá hiệu quả và xóa Redis cache (ADR-0004).
/// </summary>
internal sealed class TicketTypesChangedHandler(EventsDbContext db, IEventsCacheService? cache = null)
    : IIntegrationEventHandler<TicketTypesChangedIntegrationEvent>
{
    public async Task HandleAsync(
        TicketTypesChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // Inbox deduplication — tránh xử lý lặp
        var isNew = await db.TryAddInboxMessageAsync(
            integrationEvent.EventId,
            nameof(TicketTypesChangedHandler),
            cancellationToken);

        if (!isNew) return;

        var @event = await db.Events
            .FirstOrDefaultAsync(e => e.Id == integrationEvent.TargetEventId, cancellationToken);

        if (@event is null)
        {
            // Event chưa tồn tại trong Events module — bỏ qua (eventual consistency)
            return;
        }

        @event.UpdatePriceRange(integrationEvent.MinPrice, integrationEvent.MaxPrice);
        await db.SaveChangesAsync(cancellationToken);

        if (cache is not null)
        {
            await cache.InvalidateEventAsync(@event.Id, @event.Slug, cancellationToken);
        }
    }
}
