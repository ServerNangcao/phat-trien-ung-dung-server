namespace Ticketing.Modules.Notifications.Application.Repositories;

/// <summary>
/// Inbox port for at-most-once processing of integration events. Staging marks the event
/// as received without committing; the caller persists the staged row through its unit of
/// work so it commits atomically with the processing side effects (the email history row).
/// </summary>
public interface IInbox
{
    /// <summary>Stages an inbox row. Returns false when the event was already received.</summary>
    Task<bool> TryStageAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken = default);
}
