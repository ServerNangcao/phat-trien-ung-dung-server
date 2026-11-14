namespace Events.Internal.Features.Events.CreateEvent;

/// <summary>
/// Request DTO để tạo sự kiện mới.
/// </summary>
internal sealed record CreateEventRequest(
    Guid CategoryId,
    Guid VenueId,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt);
