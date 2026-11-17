using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.ListEvents;

/// <summary>
/// Query params để Organizer xem danh sách sự kiện của riêng mình (gồm mọi trạng thái).
/// </summary>
internal sealed record ListOrganizerEventsQuery(
    Guid OrganizerId,
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

internal sealed record OrganizerEventSummaryResponse(
    Guid Id,
    string Title,
    string Slug,
    string? CoverImageKey,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    string? RejectReason,
    decimal? MinPrice,
    decimal? MaxPrice,
    string CategoryName,
    string VenueName,
    string VenueCity,
    DateTime CreatedAt);

internal sealed class ListOrganizerEventsHandler(EventsDbContext db)
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<OrganizerEventSummaryResponse>>> HandleAsync(
        ListOrganizerEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > MaxPageSize)
            return Result.Validation<PagedResult<OrganizerEventSummaryResponse>>(
                $"Page phải >= 1, PageSize phải từ 1 đến {MaxPageSize}.");

        var q = db.Events
            .Include(e => e.Category)
            .Include(e => e.Venue)
            .Where(e => e.OrganizerId == query.OrganizerId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(e => e.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            q = q.Where(e => EF.Functions.ILike(e.Title, $"%{query.Search}%") || EF.Functions.ILike(e.Slug, $"%{query.Search}%"));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new OrganizerEventSummaryResponse(
                e.Id,
                e.Title,
                e.Slug,
                e.CoverImageKey,
                e.StartsAt,
                e.EndsAt,
                e.Status,
                e.RejectReason,
                e.MinPrice,
                e.MaxPrice,
                e.Category.Name,
                e.Venue.Name,
                e.Venue.City,
                e.CreatedAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<OrganizerEventSummaryResponse>(
            Items: items,
            TotalCount: totalCount,
            Page: query.Page,
            PageSize: query.PageSize);

        return Result.Success(result);
    }
}
