namespace CheckIn.Contracts;

public sealed record EventCheckInSummaryDto(
    Guid EventId,
    int TotalCheckedIn,
    DateTime? LastCheckInAt);

/// <summary>
/// API công khai của module CheckIn.
/// Các module khác (như Events/Analytics) gọi qua interface này (in-process).
/// </summary>
public interface ICheckInModuleApi
{
    /// <summary>
    /// Lấy tổng số lượng vé đã check-in thành công vào cổng của một sự kiện.
    /// </summary>
    Task<EventCheckInSummaryDto> GetEventCheckInSummaryAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tổng lượt check-in của nhiều sự kiện.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCheckedInCountsByEventIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default);
}
