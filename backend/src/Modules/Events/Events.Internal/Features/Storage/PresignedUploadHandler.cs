using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Storage;

/// <summary>
/// Handler tạo Presigned URL để client upload ảnh bìa trực tiếp lên Cloudflare R2.
/// Flow:
///   1. Client gọi API → nhận presigned PUT URL + object key
///   2. Client PUT file thẳng lên R2 (không qua server)
///   3. Client gọi API xác nhận → server lưu object key vào Event.CoverImageKey
/// </summary>
internal sealed class GetUploadPresignedUrlHandler(
    EventsDbContext db,
    IR2StorageService storageService)
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public async Task<Result<PresignedUploadResponse>> HandleAsync(
        Guid eventId,
        Guid organizerId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
            return Result.Validation<PresignedUploadResponse>(
                $"Content-Type không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AllowedContentTypes)}.");

        var @event = await db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (@event is null)
            return Result.NotFound<PresignedUploadResponse>("Sự kiện không tồn tại.");

        if (@event.OrganizerId != organizerId)
            return Result.Forbidden<PresignedUploadResponse>("Bạn không có quyền upload ảnh cho sự kiện này.");

        // Tạo object key theo cấu trúc: events/{eventId}/cover.{ext}
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "jpg"
        };
        var objectKey = $"events/{eventId}/cover.{extension}";

        var presignedUrl = await storageService.GeneratePresignedPutUrlAsync(
            objectKey,
            contentType,
            expiresInMinutes: 15,
            cancellationToken);

        return Result.Success(new PresignedUploadResponse(
            UploadUrl: presignedUrl,
            ObjectKey: objectKey,
            ExpiresInMinutes: 15));
    }
}

/// <summary>
/// Handler xác nhận upload thành công — lưu object key vào Event.
/// </summary>
internal sealed class ConfirmCoverImageUploadHandler(EventsDbContext db)
{
    public async Task<Result> HandleAsync(
        Guid eventId,
        Guid organizerId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return Result.Validation("ObjectKey không được để trống.");

        var @event = await db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        if (@event.OrganizerId != organizerId)
            return Result.Forbidden("Bạn không có quyền cập nhật ảnh cho sự kiện này.");

        // Validate objectKey đúng format của event này
        if (!objectKey.StartsWith($"events/{eventId}/", StringComparison.OrdinalIgnoreCase))
            return Result.Validation("ObjectKey không hợp lệ cho sự kiện này.");

        @event.SetCoverImage(objectKey);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed record PresignedUploadResponse(
    string UploadUrl,
    string ObjectKey,
    int ExpiresInMinutes);
