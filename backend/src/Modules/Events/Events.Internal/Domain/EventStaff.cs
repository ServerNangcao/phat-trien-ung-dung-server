namespace Events.Internal.Domain;

/// <summary>
/// Join entity giữa Event và Staff user.
/// Composite PK (EventId, StaffUserId) — KHÔNG kế thừa BaseEntity.
/// </summary>
internal sealed class EventStaff
{
    public Guid EventId { get; private set; }

    /// <summary>
    /// Reference tới Identity module — KHÔNG có FK cross-schema.
    /// </summary>
    public Guid StaffUserId { get; private set; }

    // Navigation property (FK internal)
    public Event Event { get; private set; } = null!;

    private EventStaff() { }

    public static EventStaff Create(Guid eventId, Guid staffUserId) => new()
    {
        EventId = eventId,
        StaffUserId = staffUserId
    };
}
