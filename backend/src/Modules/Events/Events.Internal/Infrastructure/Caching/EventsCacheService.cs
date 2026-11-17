using System.Text.Json;
using Events.Internal.Features.Events.GetEvent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Events.Internal.Infrastructure.Caching;

/// <summary>
/// Cài đặt cache invalidation cho sự kiện dựa trên IDistributedCache (Redis).
/// </summary>
internal sealed class EventsCacheService(
    IDistributedCache? cache,
    ILogger<EventsCacheService> logger) : IEventsCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string DetailKeyPrefix = "events:detail:";
    private const string SlugKeyPrefix = "events:slug:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    public async Task<EventDetailResponse?> GetEventDetailAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (cache is null) return null;
        try
        {
            var key = $"{DetailKeyPrefix}{eventId}";
            var json = await cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(json)) return null;

            return JsonSerializer.Deserialize<EventDetailResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi khi đọc Redis cache cho sự kiện {EventId}", eventId);
            return null;
        }
    }

    public async Task<EventDetailResponse?> GetEventBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (cache is null) return null;
        try
        {
            var key = $"{SlugKeyPrefix}{slug}";
            var json = await cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(json)) return null;

            return JsonSerializer.Deserialize<EventDetailResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi khi đọc Redis cache cho slug {Slug}", slug);
            return null;
        }
    }

    public async Task SetEventDetailAsync(
        EventDetailResponse response,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (cache is null) return;
        try
        {
            var json = JsonSerializer.Serialize(response, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultTtl
            };

            await Task.WhenAll(
                cache.SetStringAsync($"{DetailKeyPrefix}{response.Id}", json, options, cancellationToken),
                cache.SetStringAsync($"{SlugKeyPrefix}{response.Slug}", json, options, cancellationToken)
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi khi ghi Redis cache cho sự kiện {EventId}", response.Id);
        }
    }

    public async Task InvalidateEventAsync(
        Guid eventId,
        string? slug = null,
        CancellationToken cancellationToken = default)
    {
        if (cache is null) return;
        try
        {
            var tasks = new List<Task>
            {
                cache.RemoveAsync($"{DetailKeyPrefix}{eventId}", cancellationToken)
            };

            if (!string.IsNullOrWhiteSpace(slug))
            {
                tasks.Add(cache.RemoveAsync($"{SlugKeyPrefix}{slug}", cancellationToken));
            }

            // Bump version để invalidate toàn bộ queries danh sách (TTL 7 ngày)
            var versionOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            };
            tasks.Add(cache.SetStringAsync("events:list:version", DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), versionOptions, cancellationToken));

            await Task.WhenAll(tasks);
            logger.LogInformation("Đã invalidate cache cho sự kiện {EventId} (slug: {Slug})", eventId, slug);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi khi invalidate Redis cache cho sự kiện {EventId}", eventId);
        }
    }
}
