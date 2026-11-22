using System.Text.Json;
using CheckIn.Contracts;
using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Orders.Contracts;
using Ticketing.Contracts;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Reports;

/// <summary>
/// Báo cáo phân tích doanh thu, số vé bán theo hạng và lượt check-in của sự kiện (FR-23).
/// </summary>
internal sealed record OrganizerEventAnalyticsResponse(
    Guid EventId,
    string EventTitle,
    string Status,
    decimal TotalRevenue,
    int TotalOrdersCount,
    int TotalTicketsSold,
    int CheckedInAttendees,
    double AttendanceRatePercentage,
    IReadOnlyList<TicketTierAnalyticsItem> Tiers,
    DateTime CalculatedAt);

internal sealed record TicketTierAnalyticsItem(
    Guid TicketTypeId,
    string TierName,
    decimal Price,
    int TotalCapacity,
    int SoldCount,
    int QuantityAvailable,
    decimal Revenue);

internal sealed class GetEventAnalyticsHandler(
    EventsDbContext db,
    IOrdersModuleApi ordersApi,
    ICheckInModuleApi checkInApi,
    ITicketingModuleApi ticketingApi,
    IDistributedCache? cache = null,
    ILogger<GetEventAnalyticsHandler>? logger = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string CachePrefix = "analytics:events:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<OrganizerEventAnalyticsResponse>> HandleAsync(
        Guid eventId,
        Guid? organizerId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var eventInfo = await db.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.Id, e.OrganizerId, e.Title, e.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (eventInfo is null)
            return Result.NotFound<OrganizerEventAnalyticsResponse>("Sự kiện không tồn tại.");

        // Kiểm tra quyền theo cơ chế Fail-Closed: không phải Admin thì bắt buộc phải có organizerId hợp lệ trùng khớp
        if (!isAdmin && (!organizerId.HasValue || eventInfo.OrganizerId != organizerId.Value))
            return Result.Forbidden<OrganizerEventAnalyticsResponse>("Bạn không có quyền truy cập dữ liệu phân tích của sự kiện này.");

        var cacheKey = $"{CachePrefix}{eventId}";

        // Tối ưu cam kết p95 <= 500ms: Kiểm tra cache trước
        if (cache is not null)
        {
            try
            {
                var cachedJson = await cache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var cached = JsonSerializer.Deserialize<OrganizerEventAnalyticsResponse>(cachedJson, JsonOptions);
                    if (cached is not null) return Result.Success(cached);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex, "Lỗi khi đọc Redis cache báo cáo phân tích sự kiện {EventId}", eventId);
            }
        }

        EventRevenueSummaryDto orderRevenue;
        EventCheckInSummaryDto checkInSummary;
        IReadOnlyList<TicketTypeSummaryDto> ticketTypes;

        try
        {
            // 1. Gọi Orders module lấy doanh thu và số vé bán theo hạng
            var orderRevenueTask = ordersApi.GetEventRevenueSummaryAsync(eventId, cancellationToken);

            // 2. Gọi CheckIn module lấy số lượng người đã thực tế vào cổng
            var checkInSummaryTask = checkInApi.GetEventCheckInSummaryAsync(eventId, cancellationToken);

            // 3. Gọi Ticketing module lấy danh sách tất cả các hạng vé (sức chứa, giá, tồn kho)
            var ticketTypesTask = ticketingApi.GetTicketTypesByEventIdAsync(eventId, cancellationToken);

            await Task.WhenAll(orderRevenueTask, checkInSummaryTask, ticketTypesTask);

            orderRevenue = await orderRevenueTask;
            checkInSummary = await checkInSummaryTask;
            ticketTypes = await ticketTypesTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Gọi liên module thất bại cho sự kiện {EventId}, kích hoạt fallback từ EventStatistics", eventId);

            var fallbackStats = await db.EventStatistics
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.EventId == eventId, cancellationToken);

            if (fallbackStats is not null)
            {
                var fallbackTiers = new List<TicketTierAnalyticsItem>();
                if (!string.IsNullOrWhiteSpace(fallbackStats.TiersBreakdownJson))
                {
                    try
                    {
                        fallbackTiers = JsonSerializer.Deserialize<List<TicketTierAnalyticsItem>>(fallbackStats.TiersBreakdownJson, JsonOptions) ?? [];
                    }
                    catch { }
                }

                var fallbackAttendanceRate = fallbackStats.TotalTicketsSold > 0
                    ? Math.Min(100.0, Math.Round(((double)fallbackStats.TotalCheckedIn / fallbackStats.TotalTicketsSold) * 100, 2))
                    : 0.0;

                var fallbackResponse = new OrganizerEventAnalyticsResponse(
                    EventId: eventId,
                    EventTitle: eventInfo.Title,
                    Status: eventInfo.Status,
                    TotalRevenue: fallbackStats.TotalRevenue,
                    TotalOrdersCount: 0,
                    TotalTicketsSold: fallbackStats.TotalTicketsSold,
                    CheckedInAttendees: fallbackStats.TotalCheckedIn,
                    AttendanceRatePercentage: fallbackAttendanceRate,
                    Tiers: fallbackTiers,
                    CalculatedAt: fallbackStats.LastCalculatedAt);

                return Result.Success(fallbackResponse);
            }

            throw;
        }

        // Tổng hợp số vé bán theo từng hạng
        var tierSalesMap = orderRevenue.TicketTypesSales.ToDictionary(x => x.TicketTypeId);

        var tiersBreakdown = ticketTypes.Select(tt =>
        {
            var hasSales = tierSalesMap.TryGetValue(tt.Id, out var sales);
            var sold = hasSales && sales is not null ? sales.QuantitySold : tt.SoldQuantity;
            var rev = hasSales && sales is not null ? sales.TotalRevenue : (sold * tt.Price);

            return new TicketTierAnalyticsItem(
                TicketTypeId: tt.Id,
                TierName: tt.Name,
                Price: tt.Price,
                TotalCapacity: tt.TotalQuantity,
                SoldCount: sold,
                QuantityAvailable: tt.AvailableQuantity,
                Revenue: rev);
        }).ToList();

        // Nếu có hạng vé bán từ Orders mà không nằm trong ticketTypes query
        foreach (var extra in orderRevenue.TicketTypesSales)
        {
            if (tiersBreakdown.All(t => t.TicketTypeId != extra.TicketTypeId))
            {
                var estimatedPrice = extra.QuantitySold > 0 ? Math.Round(extra.TotalRevenue / extra.QuantitySold, 2) : 0m;
                tiersBreakdown.Add(new TicketTierAnalyticsItem(
                    TicketTypeId: extra.TicketTypeId,
                    TierName: extra.TicketTypeName,
                    Price: estimatedPrice,
                    TotalCapacity: extra.QuantitySold,
                    SoldCount: extra.QuantitySold,
                    QuantityAvailable: 0,
                    Revenue: extra.TotalRevenue));
            }
        }

        var totalSold = orderRevenue.TotalTicketsSold > 0 ? orderRevenue.TotalTicketsSold : tiersBreakdown.Sum(x => x.SoldCount);
        var totalCheckedIn = checkInSummary.TotalCheckedIn;
        var attendanceRate = totalSold > 0
            ? Math.Min(100.0, Math.Round(((double)totalCheckedIn / totalSold) * 100, 2))
            : 0.0;

        var response = new OrganizerEventAnalyticsResponse(
            EventId: eventId,
            EventTitle: eventInfo.Title,
            Status: eventInfo.Status,
            TotalRevenue: orderRevenue.TotalRevenue,
            TotalOrdersCount: orderRevenue.TotalOrdersCount,
            TotalTicketsSold: totalSold,
            CheckedInAttendees: totalCheckedIn,
            AttendanceRatePercentage: attendanceRate,
            Tiers: tiersBreakdown,
            CalculatedAt: DateTime.UtcNow);

        // Lưu / Cập nhật vào bảng EventStatistics phục vụ truy vấn định kỳ và fallback nhanh
        try
        {
            var stats = await db.EventStatistics.FirstOrDefaultAsync(s => s.EventId == eventId, cancellationToken);
            var breakdownJson = JsonSerializer.Serialize(tiersBreakdown, JsonOptions);

            if (stats is null)
            {
                stats = EventStatistics.Create(
                    eventId,
                    orderRevenue.TotalRevenue,
                    totalSold,
                    totalCheckedIn,
                    breakdownJson);
                db.EventStatistics.Add(stats);
            }
            else
            {
                stats.Update(orderRevenue.TotalRevenue, totalSold, totalCheckedIn, breakdownJson);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Lỗi khi lưu bảng EventStatistics cho sự kiện {EventId}", eventId);
        }

        // Lưu vào Redis cache
        if (cache is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(response, JsonOptions);
                await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Lỗi khi lưu Redis cache báo cáo phân tích sự kiện {EventId}", eventId);
            }
        }

        return Result.Success(response);
    }
}
