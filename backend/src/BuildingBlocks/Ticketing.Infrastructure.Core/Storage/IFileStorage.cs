namespace Ticketing.Infrastructure.Core.Storage;

/// <summary>
/// Trừu tượng hóa dịch vụ lưu trữ file (S3-compatible: MinIO, Cloudflare R2, AWS S3).
/// Dùng chung cho toàn bộ các module (Events ảnh bìa, Ticketing QR vé, Identity avatar...).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Tạo Presigned PUT URL để client upload trực tiếp lên cloud storage.
    /// </summary>
    Task<string> GeneratePresignedPutUrlAsync(
        string objectKey,
        string contentType,
        int expiresInMinutes = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo Presigned GET URL để client tải hoặc xem file private.
    /// </summary>
    Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        int expiresInMinutes = 60,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa một đối tượng khỏi kho lưu trữ.
    /// </summary>
    Task DeleteObjectAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}
