namespace Ticketing.Infrastructure.Core.Outbox;

/// <summary>
/// Inbox message recorded when an integration event is received. The unique EventId
/// gives consumers at-most-once processing for events that the outbox delivers at-least-once.
/// </summary>
public record InboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

public interface IInbox
{
    /// <summary>
    /// Record an incoming event. Returns true when the event is new (process it),
    /// false when it was already recorded (duplicate: skip).
    /// </summary>
    Task<bool> TryRecordAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken = default);
}
