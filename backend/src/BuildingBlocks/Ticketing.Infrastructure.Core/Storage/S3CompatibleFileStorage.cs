using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Ticketing.Infrastructure.Core.Storage;

/// <summary>
/// Cài đặt IFileStorage dùng AWS SDK v3 tương thích S3 (MinIO, Cloudflare R2, AWS S3).
/// </summary>
public sealed class S3CompatibleFileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageOptions _options;

    public S3CompatibleFileStorage(IAmazonS3 s3Client, IOptions<StorageOptions> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    public Task<string> GeneratePresignedPutUrlAsync(
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
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        int expiresInMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task DeleteObjectAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken);
    }
}
