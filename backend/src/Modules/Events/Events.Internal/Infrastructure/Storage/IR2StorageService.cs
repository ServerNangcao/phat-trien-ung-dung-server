namespace Events.Internal.Infrastructure.Storage;

/// <summary>
/// Interface abstraction cho R2/S3 object storage.
/// Cho phép mock trong tests và swap implementation dễ dàng.
/// </summary>
internal interface IR2StorageService
{
    /// <summary>
    /// Tạo presigned PUT URL để client upload file trực tiếp lên R2.
    /// </summary>
    Task<string> GeneratePresignedPutUrlAsync(
        string objectKey,
        string contentType,
        int expiresInMinutes = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo presigned GET URL để client tải file từ R2 (ảnh private bucket).
    /// </summary>
    Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        int expiresInMinutes = 60,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa một object khỏi R2.
    /// </summary>
    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);
}
