namespace Events.Contracts;

/// <summary>
/// Thông tin tóm tắt sự kiện phục vụ các module khác (Ticketing, Orders, Notifications).
/// </summary>
public sealed record EventBriefDto(
    Guid Id,
    string Title,
    string? CoverImageUrl,
    DateTime StartsAt,
    string VenueName,
    string VenueCity,
    Guid OrganizerId = default,
    string Status = "OnSale");

/// <summary>
/// API công khai của module Events.
/// Các module khác gọi qua interface này (in-process).
/// </summary>
public interface IEventsModuleApi
{
    /// <summary>
    /// Lấy thông tin tóm tắt của nhiều sự kiện theo danh sách ID.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, EventBriefDto>> GetEventsByIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin tóm tắt của một sự kiện theo ID.
    /// </summary>
    Task<EventBriefDto?> GetEventByIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách tóm tắt các sự kiện thuộc quyền quản lý của một Organizer.
    /// </summary>
    Task<IReadOnlyList<EventBriefDto>> GetEventsByOrganizerAsync(
        Guid organizerId,
        CancellationToken cancellationToken = default);
}
