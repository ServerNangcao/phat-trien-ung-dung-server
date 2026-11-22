using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.GetEvent;

/// <summary>
/// Response DTO đầy đủ cho một sự kiện.
/// </summary>
internal sealed record EventDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string? CoverImageUrl,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    string? RejectReason,
    decimal? MinPrice,
    decimal? MaxPrice,
    CategoryDto Category,
    VenueDto Venue,
    DateTime CreatedAt);

internal sealed record CategoryDto(Guid Id, string Name, string Slug);
internal sealed record VenueDto(Guid Id, string Name, string Address, string City, double? Latitude, double? Longitude, int? Capacity);

internal sealed class GetEventHandler(EventsDbContext db, IEventsCacheService? cache = null)
{
    public async Task<Result<EventDetailResponse>> HandleAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        if (cache is not null)
        {
            var cached = await cache.GetEventDetailAsync(eventId, cancellationToken);
            if (cached is not null)
                return Result.Success(cached);
        }

        var @event = await db.Events
            .Include(e => e.Category)
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (@event is null)
            return Result.NotFound<EventDetailResponse>("Sự kiện không tồn tại.");

        var response = MapToResponse(@event);

        if (cache is not null)
        {
            await cache.SetEventDetailAsync(response, cancellationToken: cancellationToken);
        }

        return Result.Success(response);
    }

    public async Task<Result<EventDetailResponse>> HandleBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (cache is not null)
        {
            var cached = await cache.GetEventBySlugAsync(slug, cancellationToken);
            if (cached is not null)
                return Result.Success(cached);
        }

        var @event = await db.Events
            .Include(e => e.Category)
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Slug == slug, cancellationToken);

        if (@event is null)
            return Result.NotFound<EventDetailResponse>("Sự kiện không tồn tại.");

        var response = MapToResponse(@event);

        if (cache is not null)
        {
            await cache.SetEventDetailAsync(response, cancellationToken: cancellationToken);
        }

        return Result.Success(response);
    }

    private static EventDetailResponse MapToResponse(Event @event) => new(
        @event.Id,
        @event.Title,
        @event.Slug,
        @event.Description,
        CoverImageUrl: null, // URL được tạo theo yêu cầu (presigned), không lưu URL cố định
        @event.StartsAt,
        @event.EndsAt,
        @event.Status,
        @event.RejectReason,
        @event.MinPrice,
        @event.MaxPrice,
        new CategoryDto(@event.Category.Id, @event.Category.Name, @event.Category.Slug),
        new VenueDto(
            @event.Venue.Id,
            @event.Venue.Name,
            @event.Venue.Address,
            @event.Venue.City,
            @event.Venue.Latitude,
            @event.Venue.Longitude,
            @event.Venue.Capacity),
        @event.CreatedAt);
}
