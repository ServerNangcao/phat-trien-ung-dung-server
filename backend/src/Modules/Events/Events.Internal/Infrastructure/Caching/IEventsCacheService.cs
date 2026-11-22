using Events.Internal.Features.Events.GetEvent;

namespace Events.Internal.Infrastructure.Caching;

/// <summary>
/// Dịch vụ quản lý bộ nhớ đệm (Redis / IDistributedCache) cho module Events theo chuẩn ADR-0004.
/// </summary>
internal interface IEventsCacheService
{
    Task<EventDetailResponse?> GetEventDetailAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventDetailResponse?> GetEventBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task SetEventDetailAsync(EventDetailResponse response, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task InvalidateEventAsync(Guid eventId, string? slug = null, CancellationToken cancellationToken = default);
}
