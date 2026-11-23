using Events.Contracts;
using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Events.Internal.Api;

internal sealed class EventsModuleApi(
    EventsDbContext db,
    IR2StorageService? storageService = null,
    ILogger<EventsModuleApi>? logger = null) : IEventsModuleApi
{
    public async Task<IReadOnlyDictionary<Guid, EventBriefDto>> GetEventsByIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default)
    {
        var idList = eventIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, EventBriefDto>();
        }

        var rows = await db.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Where(e => idList.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.CoverImageKey,
                e.StartsAt,
                VenueName = e.Venue != null ? e.Venue.Name : string.Empty,
                VenueCity = e.Venue != null ? e.Venue.City : string.Empty
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, EventBriefDto>(rows.Count);
        foreach (var row in rows)
        {
            var coverImageUrl = await ResolveCoverImageUrlAsync(row.Id, row.CoverImageKey, cancellationToken);
            result[row.Id] = new EventBriefDto(
                row.Id,
                row.Title,
                coverImageUrl,
                row.StartsAt,
                row.VenueName,
                row.VenueCity);
        }

        return result;
    }

    public async Task<EventBriefDto?> GetEventByIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var events = await GetEventsByIdsAsync([eventId], cancellationToken);
        return events.GetValueOrDefault(eventId);
    }

    private async Task<string?> ResolveCoverImageUrlAsync(
        Guid eventId,
        string? coverImageKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(coverImageKey))
        {
            return null;
        }

        if (coverImageKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            coverImageKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return coverImageKey;
        }

        if (storageService is null)
        {
            return null;
        }

        try
        {
            return await storageService.GeneratePresignedGetUrlAsync(
                coverImageKey,
                expiresInMinutes: 60,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Không thể tạo presigned URL cho ảnh bìa sự kiện {EventId}",
                eventId);
            return null;
        }
    }
}
