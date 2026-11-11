using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Events.Internal.Infrastructure.Storage;

/// <summary>
/// Cài đặt IR2StorageService dùng AWS SDK trỏ vào Cloudflare R2.
/// R2 tương thích hoàn toàn với S3 API — chỉ cần đổi ServiceURL thành endpoint R2.
/// </summary>
internal sealed class R2StorageService : IR2StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2Options _options;

    public R2StorageService(IAmazonS3 s3Client, IOptions<R2Options> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    public async Task<string> GeneratePresignedPutUrlAsync(
        string objectKey,
        string contentType,
        int expiresInMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
        };

        // GetPreSignedURL là synchronous trong AWS SDK v3
        var url = await Task.FromResult(_s3Client.GetPreSignedURL(request));
        return url;
    }

    public async Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        int expiresInMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
        };

        var url = await Task.FromResult(_s3Client.GetPreSignedURL(request));
        return url;
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
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
