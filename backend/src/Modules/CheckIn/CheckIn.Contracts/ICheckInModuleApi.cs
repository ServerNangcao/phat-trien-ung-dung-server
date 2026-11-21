namespace CheckIn.Contracts;

public sealed record CheckInRequestDto(Guid EventId, string QrPayload, string? Gate);

public sealed record CheckInResultDto(
    string Status,
    Guid? TicketId,
    string? TicketTypeName,
    DateTime? ScannedAtUtc,
    string Message);

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
    Task<int> GetCheckedInCountAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<CheckInResultDto> CheckInAsync(CheckInRequestDto request, string staffId, CancellationToken cancellationToken = default);
    Task AssignStaffAsync(Guid eventId, string staffId, CancellationToken cancellationToken = default);

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
