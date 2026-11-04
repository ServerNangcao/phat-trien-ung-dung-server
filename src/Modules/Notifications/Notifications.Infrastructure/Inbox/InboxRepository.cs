using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Notifications.Application.Repositories;

namespace Ticketing.Modules.Notifications.Infrastructure.Inbox;

/// <summary>
/// Inbox persistence for received integration events. Staging uses a pre-check for
/// single-instance dedupe; the unique index on EventId closes the concurrency race at
/// commit time (the losing UnitOfWork.CommitAsync reports a duplicate Result).
/// </summary>
public sealed class InboxRepository : IInbox
{
    private readonly NotificationsDbContext _dbContext;

    public InboxRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryStageAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken = default)
    {
        var alreadyReceived = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == eventId, cancellationToken);

        if (alreadyReceived)
            return false;

        _dbContext.InboxMessages.Add(new Ticketing.Infrastructure.Core.Outbox.InboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            Payload = payload
        });

        return true;
    }
}
