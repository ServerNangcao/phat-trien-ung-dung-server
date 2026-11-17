using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.ListEvents;

/// <summary>
/// Query params để lọc + phân trang danh sách sự kiện công khai (OnSale).
/// </summary>
internal sealed record ListEventsQuery(
    string? Status = null,
    Guid? CategoryId = null,
    string? City = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

internal sealed record EventSummaryResponse(
    Guid Id,
    string Title,
    string Slug,
    string? CoverImageKey,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    string CategoryName,
    string VenueCity);

internal sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

internal sealed class ListEventsHandler(EventsDbContext db)
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResult<EventSummaryResponse>>> HandleAsync(
        ListEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > MaxPageSize)
            return Result.Validation<PagedResult<EventSummaryResponse>>(
                $"Page phải >= 1, PageSize phải từ 1 đến {MaxPageSize}.");

        var q = db.Events
            .Include(e => e.Category)
            .Include(e => e.Venue)
            .AsQueryable();

        // Lọc theo Status (mặc định chỉ show OnSale nếu không truyền)
        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(e => e.Status == query.Status);
        else
            q = q.Where(e => e.Status == EventStatus.OnSale);

        // Lọc theo Category
        if (query.CategoryId.HasValue)
            q = q.Where(e => e.CategoryId == query.CategoryId.Value);

        // Lọc theo thành phố
        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(e => e.Venue.City == query.City);

        // Lọc theo khoảng giá
        if (query.MinPrice.HasValue)
            q = q.Where(e => e.MaxPrice == null || e.MaxPrice >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            q = q.Where(e => e.MinPrice == null || e.MinPrice <= query.MaxPrice.Value);

        // Full-text search theo title (case-insensitive)
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(e => EF.Functions.ILike(e.Title, $"%{query.Search}%"));

        // Sắp xếp theo thời gian bắt đầu
        q = q.OrderBy(e => e.StartsAt);

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new EventSummaryResponse(
                e.Id,
                e.Title,
                e.Slug,
                e.CoverImageKey,
                e.StartsAt,
                e.EndsAt,
                e.Status,
                e.MinPrice,
                e.MaxPrice,
                e.Category.Name,
                e.Venue.City))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<EventSummaryResponse>(
            items,
            totalCount,
            query.Page,
            query.PageSize));
    }
}
