using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Ticketing.Infrastructure.Core.Storage;

namespace Events.Internal.Infrastructure.Storage;

/// <summary>
/// Cài đặt IR2StorageService dùng IFileStorage hoặc AWS SDK trỏ vào Cloudflare R2 / MinIO.
/// </summary>
internal sealed class R2StorageService : IR2StorageService
{
    private readonly IFileStorage? _fileStorage;
    private readonly IAmazonS3? _s3Client;
    private readonly R2Options? _options;

    public R2StorageService(
        IFileStorage? fileStorage = null,
        IAmazonS3? s3Client = null,
        IOptions<R2Options>? options = null)
    {
        _fileStorage = fileStorage;
        _s3Client = s3Client;
        _options = options?.Value;
    }

    public async Task<string> GeneratePresignedPutUrlAsync(
        string objectKey,
        string contentType,
        int expiresInMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        if (_fileStorage is not null)
            return await _fileStorage.GeneratePresignedPutUrlAsync(objectKey, contentType, expiresInMinutes, cancellationToken);

        if (_s3Client is null || _options is null)
            throw new InvalidOperationException("Chưa cấu hình dịch vụ lưu trữ.");

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
        };

        return await Task.FromResult(_s3Client.GetPreSignedURL(request));
    }

    public async Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        int expiresInMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        if (_fileStorage is not null)
            return await _fileStorage.GeneratePresignedGetUrlAsync(objectKey, expiresInMinutes, cancellationToken);

        if (_s3Client is null || _options is null)
            throw new InvalidOperationException("Chưa cấu hình dịch vụ lưu trữ.");

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
        };

        return await Task.FromResult(_s3Client.GetPreSignedURL(request));
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (_fileStorage is not null)
        {
            await _fileStorage.DeleteObjectAsync(objectKey, cancellationToken);
            return;
        }

        if (_s3Client is null || _options is null)
            throw new InvalidOperationException("Chưa cấu hình dịch vụ lưu trữ.");

        var request = new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken);
    }
}

/// <summary>
/// Cấu hình kết nối Cloudflare R2.
/// Đọc từ appsettings: "R2": { "AccountId": "...", "AccessKey": "...", ... }
/// </summary>
internal sealed class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// URL endpoint R2 theo format: https://{AccountId}.r2.cloudflarestorage.com
    /// </summary>
    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}
