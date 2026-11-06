namespace Ticketing.Infrastructure.Core.Outbox;

/// <summary>
/// Inbox message recorded when an integration event is received. The unique EventId
/// gives consumers at-most-once processing for events that the outbox delivers at-least-once.
/// The inbox PORT lives in each consuming module's Application layer; this entity is the
/// shared persistence shape.
/// </summary>
public record InboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}
