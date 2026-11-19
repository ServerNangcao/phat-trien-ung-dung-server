namespace CheckIn.Contracts;

public sealed record CheckInRequestDto(Guid EventId, string QrPayload, string? Gate);

public sealed record CheckInResultDto(
    string Status,
    Guid? TicketId,
    string? TicketTypeName,
    DateTime? ScannedAtUtc,
    string Message);

/// <summary>
/// API công khai của module CheckIn.
/// Các module khác gọi qua interface này (in-process).
/// </summary>
public interface ICheckInModuleApi
{
    Task<int> GetCheckedInCountAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<CheckInResultDto> CheckInAsync(CheckInRequestDto request, string staffId, CancellationToken cancellationToken = default);
    Task AssignStaffAsync(Guid eventId, string staffId, CancellationToken cancellationToken = default);
}
