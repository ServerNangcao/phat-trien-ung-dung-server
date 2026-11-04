using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Outbox;

namespace Ticketing.Modules.Notifications.Infrastructure.Inbox;

/// <summary>
/// Inbox persistence for received integration events. The pre-check covers single-instance
/// dedupe; the unique index on EventId closes the race between concurrent deliveries
/// (a losing insert surfaces as DbUpdateException).
/// </summary>
public sealed class InboxRepository : IInbox
{
    private readonly NotificationsDbContext _dbContext;

    public InboxRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryRecordAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken = default)
    {
        var alreadyReceived = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == eventId, cancellationToken);

        if (alreadyReceived)
            return false;

        _dbContext.InboxMessages.Add(new InboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            Payload = payload
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
