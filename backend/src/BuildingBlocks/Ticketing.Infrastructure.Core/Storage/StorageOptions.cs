namespace Ticketing.Infrastructure.Core.Storage;

/// <summary>
/// Cấu hình lưu trữ tệp tin tương thích S3 (MinIO, Cloudflare R2, AWS S3).
/// Cho phép cấu hình linh hoạt từ appsettings.json.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Nhà cung cấp: "MinIO", "R2", "S3", hoặc tự động suy luận.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// URL máy chủ S3/MinIO (ví dụ: http://localhost:9000).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Account ID dùng riêng cho Cloudflare R2.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Access Key (hoặc minioadmin).
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret Key.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Tên bucket mặc định.
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Bắt buộc dùng path style (true cho MinIO và Cloudflare R2).
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Region AWS S3 nếu dùng AWS trực tiếp (mặc định "auto" hoặc "us-east-1").
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Tự động phân giải ServiceURL tương ứng với cấu hình R2 hoặc MinIO.
    /// </summary>
    public string ResolvedServiceUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ServiceUrl))
                return ServiceUrl;

            if (!string.IsNullOrWhiteSpace(AccountId))
                return $"https://{AccountId}.r2.cloudflarestorage.com";

            return string.Empty;
        }
    }
}
