using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.ChangeEventStatus;

/// <summary>
/// Handler xử lý chuyển đổi trạng thái sự kiện theo state machine:
/// Draft → PendingApproval → OnSale | Rejected → (sửa) → PendingApproval
/// OnSale → Ended | Cancelled
/// </summary>
internal sealed class ChangeEventStatusHandler(EventsDbContext db, IEventsCacheService? cache = null)
{
    /// <summary>Organizer nộp sự kiện để duyệt.</summary>
    public async Task<Result> SubmitForApprovalAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        if (@event.OrganizerId != organizerId)
            return Result.Forbidden("Bạn không có quyền thực hiện hành động này.");

        if (@event.Status != EventStatus.Draft && @event.Status != EventStatus.Rejected)
            return Result.Conflict($"Chỉ có thể nộp duyệt sự kiện ở trạng thái Draft hoặc Rejected. Hiện tại: {@event.Status}.");

        @event.SubmitForApproval();
        await db.SaveChangesAsync(cancellationToken);

        if (cache is not null)
            await cache.InvalidateEventAsync(@event.Id, @event.Slug, cancellationToken);

        return Result.Success();
    }

    /// <summary>Admin duyệt sự kiện → OnSale.</summary>
    public async Task<Result> ApproveAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        if (@event.Status != EventStatus.PendingApproval)
            return Result.Conflict($"Chỉ có thể duyệt sự kiện ở trạng thái PendingApproval. Hiện tại: {@event.Status}.");

        @event.Approve();
        await db.SaveChangesAsync(cancellationToken);

        if (cache is not null)
            await cache.InvalidateEventAsync(@event.Id, @event.Slug, cancellationToken);

        return Result.Success();
    }

    /// <summary>Admin từ chối sự kiện.</summary>
    public async Task<Result> RejectAsync(
        Guid eventId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Validation("Lý do từ chối không được để trống.");

        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        if (@event.Status != EventStatus.PendingApproval)
            return Result.Conflict($"Chỉ có thể từ chối sự kiện ở trạng thái PendingApproval. Hiện tại: {@event.Status}.");

        @event.Reject(reason);
        await db.SaveChangesAsync(cancellationToken);

        if (cache is not null)
            await cache.InvalidateEventAsync(@event.Id, @event.Slug, cancellationToken);

        return Result.Success();
    }

    /// <summary>Kết thúc sự kiện (manual hoặc từ job scheduler).</summary>
    public async Task<Result> EndAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        if (@event.Status != EventStatus.OnSale)
            return Result.Conflict($"Chỉ có thể kết thúc sự kiện ở trạng thái OnSale. Hiện tại: {@event.Status}.");

        @event.End();
        await db.SaveChangesAsync(cancellationToken);

        if (cache is not null)
            await cache.InvalidateEventAsync(@event.Id, @event.Slug, cancellationToken);

        return Result.Success();
    }

    /// <summary>Hủy sự kiện (Organizer của sự kiện hoặc Admin).</summary>
    public async Task<Result> CancelAsync(
        Guid eventId,
        Guid? organizerId = null,
        bool isAdmin = false,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        if (!isAdmin)
        {
            if (!organizerId.HasValue || @event.OrganizerId != organizerId.Value)
                return Result.Forbidden("Bạn không có quyền thực hiện hành động này.");
        }

        if (@event.Status == EventStatus.Ended || @event.Status == EventStatus.Cancelled || @event.Status == EventStatus.Rejected)
            return Result.Conflict($"Không thể hủy sự kiện đã ở trạng thái {@event.Status}.");

        @event.Cancel(reason);
        await db.SaveChangesAsync(cancellationToken);

        if (cache is not null)
            await cache.InvalidateEventAsync(@event.Id, @event.Slug, cancellationToken);

        return Result.Success();
    }
}
