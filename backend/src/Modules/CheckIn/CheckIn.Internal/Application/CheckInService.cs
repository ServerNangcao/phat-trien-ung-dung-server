using Microsoft.EntityFrameworkCore;
using CheckIn.Contracts;
using CheckIn.Internal.Domain;
using CheckIn.Internal.Infrastructure;
using Ticketing.Contracts;

namespace CheckIn.Internal.Application;

internal sealed class CheckInService : ICheckInModuleApi
{
    private readonly CheckInDbContext _db;
    private readonly ITicketingModuleApi _ticketingApi;

    public CheckInService(
        CheckInDbContext db,
        ITicketingModuleApi ticketingApi)
    {
        _db = db;
        _ticketingApi = ticketingApi;
    }

    public async Task<int> GetCheckedInCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CheckInLogs
            .AsNoTracking()
            .CountAsync(x => x.EventId == eventId && x.Status == "Valid", cancellationToken);
    }

    public async Task<CheckInResultDto> CheckInAsync(
        CheckInRequestDto request,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra phân công nhân viên nếu có thiết lập
        var hasAssignments = await _db.StaffAssignments
            .AnyAsync(x => x.EventId == request.EventId, cancellationToken);

        if (hasAssignments)
        {
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(x => x.EventId == request.EventId && x.StaffId == staffId, cancellationToken);

            if (!isAssigned)
            {
                return new CheckInResultDto(
                    "Forbidden",
                    null,
                    null,
                    DateTime.UtcNow,
                    "Nhân viên chưa được phân công kiểm soát sự kiện này.");
            }
        }

        // 2. Gọi sang module Ticketing để xác thực QR HMAC và đổi trạng thái 'Used' nguyên tử
        var redeem = await _ticketingApi.RedeemTicketAsync(
            request.QrPayload,
            request.EventId,
            staffId,
            cancellationToken);

        var status = redeem.Status switch
        {
            RedeemTicketStatus.Valid => "Valid",
            RedeemTicketStatus.AlreadyUsed => "AlreadyUsed",
            _ => "Invalid"
        };

        var gate = string.IsNullOrWhiteSpace(request.Gate) ? "Cổng chính" : request.Gate.Trim();

        // 3. Ghi audit log vào checkin_logs
        var log = new CheckInLog
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            TicketId = redeem.TicketId,
            StaffId = staffId,
            Status = status,
            Gate = gate,
            ScannedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.CheckInLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        return new CheckInResultDto(
            status,
            redeem.TicketId,
            redeem.TicketTypeName,
            log.ScannedAtUtc,
            redeem.Message ?? (status == "Valid" ? "Vé hợp lệ" : "Vé không hợp lệ"));
    }

    public async Task AssignStaffAsync(
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.StaffAssignments
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.StaffId == staffId, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        _db.StaffAssignments.Add(new StaffAssignment
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            StaffId = staffId,
            AssignedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}

