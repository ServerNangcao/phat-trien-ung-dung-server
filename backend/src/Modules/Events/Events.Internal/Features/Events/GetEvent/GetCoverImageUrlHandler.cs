using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.GetEvent;

/// <summary>
/// Handler tạo presigned GET URL cho ảnh bìa sự kiện.
/// URL có thời hạn ngắn (60 phút) vì bucket R2 là private.
/// </summary>
internal sealed class GetCoverImageUrlHandler(
    EventsDbContext db,
    IR2StorageService storageService)
{
    public async Task<Result<string>> HandleAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events
            .Select(e => new { e.Id, e.CoverImageKey })
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (@event is null)
            return Result.NotFound<string>("Sự kiện không tồn tại.");

        if (string.IsNullOrEmpty(@event.CoverImageKey))
            return Result.NotFound<string>("Sự kiện chưa có ảnh bìa.");

        var url = await storageService.GeneratePresignedGetUrlAsync(
            @event.CoverImageKey,
            expiresInMinutes: 60,
            cancellationToken);

        return Result.Success(url);
    }
}
