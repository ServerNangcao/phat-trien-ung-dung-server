using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Reports;

/// <summary>
/// Response tổng hợp báo cáo sự kiện theo Organizer.
/// </summary>
internal sealed record OrganizerReportResponse(
    Guid OrganizerId,
    int TotalEvents,
    int DraftCount,
    int PendingApprovalCount,
    int OnSaleCount,
    int EndedCount,
    int RejectedCount,
    IReadOnlyList<EventReportItem> Events);

internal sealed record EventReportItem(
    Guid Id,
    string Title,
    string Status,
    DateTime StartsAt,
    DateTime EndsAt,
    decimal? MinPrice,
    decimal? MaxPrice,
    string CategoryName,
    string VenueCity);

/// <summary>
/// Handler tạo báo cáo tổng hợp sự kiện theo Organizer.
/// Dùng để Organizer xem dashboard quản lý sự kiện của mình.
/// </summary>
internal sealed class GetOrganizerReportHandler(EventsDbContext db)
{
    public async Task<Result<OrganizerReportResponse>> HandleAsync(
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        var events = await db.Events
            .Include(e => e.Category)
            .Include(e => e.Venue)
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = events.Select(e => new EventReportItem(
            e.Id,
            e.Title,
            e.Status,
            e.StartsAt,
            e.EndsAt,
            e.MinPrice,
            e.MaxPrice,
            e.Category.Name,
            e.Venue.City)).ToList();

        var report = new OrganizerReportResponse(
            OrganizerId: organizerId,
            TotalEvents: events.Count,
            DraftCount: events.Count(e => e.Status == Domain.EventStatus.Draft),
            PendingApprovalCount: events.Count(e => e.Status == Domain.EventStatus.PendingApproval),
            OnSaleCount: events.Count(e => e.Status == Domain.EventStatus.OnSale),
            EndedCount: events.Count(e => e.Status == Domain.EventStatus.Ended),
            RejectedCount: events.Count(e => e.Status == Domain.EventStatus.Rejected),
            Events: items);

        return Result.Success(report);
    }
}

/// <summary>
/// Handler tạo báo cáo admin — tổng hợp toàn bộ sự kiện.
/// </summary>
internal sealed class GetAdminReportHandler(EventsDbContext db)
{
    public async Task<Result<AdminReportResponse>> HandleAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Events.AsQueryable();

        if (from.HasValue)
            query = query.Where(e => e.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.CreatedAt <= to.Value);

        var totalEvents = await query.CountAsync(cancellationToken);
        var byStatus = await query
            .GroupBy(e => e.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var byCategory = await query
            .Include(e => e.Category)
            .GroupBy(e => e.Category.Name)
            .Select(g => new CategoryCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var byCity = await query
            .Include(e => e.Venue)
            .GroupBy(e => e.Venue.City)
            .Select(g => new CityCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminReportResponse(
            TotalEvents: totalEvents,
            From: from,
            To: to,
            ByStatus: byStatus,
            ByCategory: byCategory,
            TopCities: byCity));
    }
}

internal sealed record AdminReportResponse(
    int TotalEvents,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<CityCount> TopCities);

internal sealed record StatusCount(string Status, int Count);
internal sealed record CategoryCount(string Category, int Count);
internal sealed record CityCount(string City, int Count);
