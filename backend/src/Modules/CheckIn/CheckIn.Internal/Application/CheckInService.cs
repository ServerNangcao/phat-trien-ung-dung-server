using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CheckIn.Contracts;
using CheckIn.Internal.Domain;
using CheckIn.Internal.Infrastructure;
using Ticketing.Contracts;

namespace CheckIn.Internal.Application;

/// <summary>
/// [TV4 - Đồng]: Service xử lý kiểm soát vé tại cổng và lưu vết check-in.
/// </summary>
internal sealed class CheckInService : ICheckInModuleApi
{
    private readonly CheckInDbContext _db;
    private readonly ITicketingModuleApi _ticketingApi;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(
        CheckInDbContext db,
        ITicketingModuleApi ticketingApi,
        ILogger<CheckInService> logger)
    {
        _db = db;
        _ticketingApi = ticketingApi;
        _logger = logger;
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy số lượng người tham dự đã check-in thành công vào sự kiện.
    /// </summary>
    public async Task<int> GetCheckedInCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CheckInLogs
            .AsNoTracking()
            .CountAsync(x => x.EventId == eventId && x.Result == CheckInResult.Valid, cancellationToken);
    }

    /// <summary>
    /// [TV4 - Đồng]: Soát vé tại cổng - Gọi Ticketing xác thực chữ ký HMAC và đổi trạng thái sang 'Used',
    /// lưu log kiểm soát và chống quay vòng vé.
    /// </summary>
    public async Task<CheckInResultDto> CheckInAsync(
        CheckInRequestDto request,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi sang Ticketing module để xác thực chữ ký số HMAC và đổi trạng thái nguyên tử
        var redeem = await _ticketingApi.RedeemTicketAsync(
            request.QrPayload,
            request.EventId,
            staffId,
            cancellationToken);

        var resultStatus = redeem.Status switch
        {
            RedeemTicketStatus.Valid => CheckInResult.Valid,
            RedeemTicketStatus.AlreadyUsed => CheckInResult.AlreadyUsed,
            _ => CheckInResult.Invalid
        };

        if (!Guid.TryParse(staffId, out var staffGuid))
        {
            staffGuid = Guid.Empty;
        }

        // 2. Ghi audit log vào CheckInLogs
        var log = CheckInLog.Create(
            redeem.TicketId,
            request.EventId,
            staffGuid,
            resultStatus,
            request.Gate);

        _db.CheckInLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[TV4 - CheckIn]: Soát vé sự kiện {EventId}, kết quả: {Result}, nhân viên: {StaffId}",
            request.EventId, resultStatus, staffId);

        return new CheckInResultDto(
            resultStatus,
            redeem.TicketId,
            redeem.TicketTypeName,
            log.ScannedAt,
            redeem.Message ?? (resultStatus == CheckInResult.Valid ? "Vé hợp lệ" : "Vé không hợp lệ"));
    }

    /// <summary>
    /// Phân công nhân viên soát vé cho sự kiện.
    /// </summary>
    public Task AssignStaffAsync(
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Phân công nhân viên {StaffId} cho sự kiện {EventId}", staffId, eventId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Lấy tổng số lượng vé đã check-in thành công vào cổng của một sự kiện.
    /// </summary>
    public async Task<EventCheckInSummaryDto> GetEventCheckInSummaryAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var validLogsQuery = _db.CheckInLogs
            .Where(x => x.EventId == eventId && x.Result == CheckInResult.Valid);

        var totalCheckedIn = await validLogsQuery.CountAsync(cancellationToken);
        var lastScanned = totalCheckedIn > 0
            ? await validLogsQuery.MaxAsync(x => (DateTime?)x.ScannedAt, cancellationToken)
            : null;

        return new EventCheckInSummaryDto(
            EventId: eventId,
            TotalCheckedIn: totalCheckedIn,
            LastCheckInAt: lastScanned);
    }

    /// <summary>
    /// Lấy tổng lượt check-in của nhiều sự kiện.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> GetCheckedInCountsByEventIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default)
    {
        var idList = eventIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await _db.CheckInLogs
            .Where(x => idList.Contains(x.EventId) && x.Result == CheckInResult.Valid)
            .GroupBy(x => x.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, cancellationToken);

        return counts;
    }
}
