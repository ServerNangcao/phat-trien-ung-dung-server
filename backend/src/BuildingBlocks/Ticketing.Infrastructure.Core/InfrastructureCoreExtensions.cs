using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.Infrastructure.Core.Storage;
using Ticketing.SharedKernel;

namespace Ticketing.Infrastructure.Core;

/// <summary>
/// Extension method để Host đăng ký hạ tầng dùng chung:
/// IClock, OutboxWorker, OutboxCleanupWorker, OutboxOptions, IFileStorage (MinIO / R2 / S3).
/// Gọi một lần trong Program.cs.
/// </summary>
public static class InfrastructureCoreExtensions
{
    public static IServiceCollection AddInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // System clock
        services.AddSingleton<IClock, SystemClock>();

        // Outbox options
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        // Background workers
        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<OutboxCleanupWorker>();

        // Storage setup (hỗ trợ chuyển đổi cấu hình MinIO Local Dev vs Cloudflare R2 Production)
        services.AddFileStorage(configuration);

        // Distributed Cache (Redis nếu có cấu hình, hoặc fallback sang in-memory distributed cache)
        var redisConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddStackExchangeRedisCache(opts =>
            {
                opts.Configuration = redisConn;
                opts.InstanceName = "ticketing:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    public static IServiceCollection AddFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var storageOptions = ResolveStorageOptions(configuration);
        services.Configure<StorageOptions>(opts =>
        {
            opts.Provider = storageOptions.Provider;
            opts.ServiceUrl = storageOptions.ServiceUrl;
            opts.AccountId = storageOptions.AccountId;
            opts.AccessKey = storageOptions.AccessKey;
            opts.SecretKey = storageOptions.SecretKey;
            opts.BucketName = storageOptions.BucketName;
            opts.ForcePathStyle = storageOptions.ForcePathStyle;
            opts.Region = storageOptions.Region;
        });

        if (!string.IsNullOrWhiteSpace(storageOptions.AccessKey) &&
            (!string.IsNullOrWhiteSpace(storageOptions.ResolvedServiceUrl) || !string.IsNullOrWhiteSpace(storageOptions.Region)))
        {
            services.AddSingleton<IAmazonS3>(_ =>
            {
                var config = new AmazonS3Config
                {
                    ForcePathStyle = storageOptions.ForcePathStyle
                };

                if (!string.IsNullOrWhiteSpace(storageOptions.ResolvedServiceUrl))
                {
                    config.ServiceURL = storageOptions.ResolvedServiceUrl;
                }

                return new AmazonS3Client(storageOptions.AccessKey, storageOptions.SecretKey, config);
            });

            services.AddScoped<IFileStorage, S3CompatibleFileStorage>();
        }

        return services;
    }

    private static StorageOptions ResolveStorageOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        // Fallback đọc từ section "S3" nếu có (ví dụ cấu hình MinIO cục bộ)
        var s3Section = configuration.GetSection("S3");
        if (s3Section.Exists())
        {
            if (string.IsNullOrWhiteSpace(options.ServiceUrl))
                options.ServiceUrl = s3Section["ServiceUrl"];
            if (string.IsNullOrWhiteSpace(options.AccessKey))
                options.AccessKey = s3Section["AccessKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(options.SecretKey))
                options.SecretKey = s3Section["SecretKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(options.BucketName))
                options.BucketName = s3Section["BucketName"] ?? string.Empty;
            if (bool.TryParse(s3Section["ForcePathStyle"], out var fps))
                options.ForcePathStyle = fps;
        }

        // Fallback đọc từ section "R2" nếu có (Cloudflare R2)
        var r2Section = configuration.GetSection("R2");
        if (r2Section.Exists())
        {
            if (string.IsNullOrWhiteSpace(options.AccountId))
                options.AccountId = r2Section["AccountId"];
            if (string.IsNullOrWhiteSpace(options.AccessKey))
                options.AccessKey = r2Section["AccessKey"] ?? options.AccessKey;
            if (string.IsNullOrWhiteSpace(options.SecretKey))
                options.SecretKey = r2Section["SecretKey"] ?? options.SecretKey;
            if (string.IsNullOrWhiteSpace(options.BucketName))
                options.BucketName = r2Section["BucketName"] ?? options.BucketName;
        }

        return options;
    }
}
