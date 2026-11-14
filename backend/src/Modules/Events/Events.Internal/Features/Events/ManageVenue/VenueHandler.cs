using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.ManageVenue;

// ─── Venue CRUD ─────────────────────────────────────────────────────────────

internal sealed record CreateVenueRequest(
    string Name,
    string Address,
    string City,
    double? Latitude = null,
    double? Longitude = null,
    int? Capacity = null);

internal sealed record VenueResponse(
    Guid Id,
    string Name,
    string Address,
    string City,
    double? Latitude,
    double? Longitude,
    int? Capacity);

internal sealed class VenueHandler(EventsDbContext db)
{
    public async Task<Result<Guid>> CreateAsync(
        CreateVenueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Validation<Guid>("Tên venue không được để trống.");

        if (request.Name.Length > 300)
            return Result.Validation<Guid>("Tên venue không được quá 300 ký tự.");

        if (string.IsNullOrWhiteSpace(request.Address))
            return Result.Validation<Guid>("Địa chỉ không được để trống.");

        if (string.IsNullOrWhiteSpace(request.City))
            return Result.Validation<Guid>("Thành phố không được để trống.");

        var venue = Domain.Venue.Create(
            request.Name,
            request.Address,
            request.City,
            request.Latitude,
            request.Longitude,
            request.Capacity);

        db.Venues.Add(venue);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(venue.Id);
    }

    public async Task<Result<IReadOnlyList<VenueResponse>>> ListAsync(
        string? city = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Venues.AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(v => v.City == city);

        var venues = await query
            .OrderBy(v => v.City)
            .ThenBy(v => v.Name)
            .Select(v => new VenueResponse(v.Id, v.Name, v.Address, v.City, v.Latitude, v.Longitude, v.Capacity))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<VenueResponse>>(venues);
    }

    public async Task<Result<VenueResponse>> GetAsync(
        Guid venueId,
        CancellationToken cancellationToken = default)
    {
        var venue = await db.Venues
            .Select(v => new VenueResponse(v.Id, v.Name, v.Address, v.City, v.Latitude, v.Longitude, v.Capacity))
            .FirstOrDefaultAsync(v => v.Id == venueId, cancellationToken);

        if (venue is null)
            return Result.NotFound<VenueResponse>("Venue không tồn tại.");

        return Result.Success(venue);
    }
}
