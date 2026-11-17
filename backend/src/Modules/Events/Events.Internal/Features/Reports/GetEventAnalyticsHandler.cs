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
internal sealed record EventAnalyticsResponse(
    Guid EventId,
    string EventTitle,
    string Status,
    decimal TotalRevenue,
    int TotalOrdersCount,
    int TotalTicketsSold,
    int TotalCheckedIn,
    double AttendanceRatePercentage,
    IReadOnlyList<EventTierAnalyticsDto> TiersBreakdown,
    DateTime CalculatedAt);

internal sealed record EventTierAnalyticsDto(
    Guid TicketTypeId,
    string TicketTypeName,
    decimal Price,
    int TotalCapacity,
    int QuantitySold,
    int QuantityAvailable,
    decimal TotalRevenue);

internal sealed class GetEventAnalyticsHandler(
    EventsDbContext db,
    IOrdersModuleApi ordersApi,
    ICheckInModuleApi checkInApi,
    ITicketingModuleApi ticketingApi,
    IDistributedCache? cache,
    ILogger<GetEventAnalyticsHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string CachePrefix = "analytics:events:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<EventAnalyticsResponse>> HandleAsync(
        Guid eventId,
        Guid? organizerId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound<EventAnalyticsResponse>("Sự kiện không tồn tại.");

        if (!isAdmin && organizerId.HasValue && @event.OrganizerId != organizerId.Value)
            return Result.Forbidden<EventAnalyticsResponse>("Bạn không có quyền truy cập dữ liệu phân tích của sự kiện này.");

        var cacheKey = $"{CachePrefix}{eventId}";

        // Tối ưu cam kết p95 <= 500ms: Kiểm tra cache trước
        if (cache is not null)
        {
            try
            {
                var cachedJson = await cache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var cached = JsonSerializer.Deserialize<EventAnalyticsResponse>(cachedJson, JsonOptions);
                    if (cached is not null) return Result.Success(cached);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Lỗi khi đọc Redis cache báo cáo phân tích sự kiện {EventId}", eventId);
            }
        }

        // 1. Gọi Orders module lấy doanh thu và số vé bán theo hạng
        var orderRevenueTask = ordersApi.GetEventRevenueSummaryAsync(eventId, cancellationToken);

        // 2. Gọi CheckIn module lấy số lượng người đã thực tế vào cổng
        var checkInSummaryTask = checkInApi.GetEventCheckInSummaryAsync(eventId, cancellationToken);

        // 3. Gọi Ticketing module lấy danh sách tất cả các hạng vé (sức chứa, giá, tồn kho)
        var ticketTypesTask = ticketingApi.GetTicketTypesByEventIdAsync(eventId, cancellationToken);

        await Task.WhenAll(orderRevenueTask, checkInSummaryTask, ticketTypesTask);

        var orderRevenue = await orderRevenueTask;
        var checkInSummary = await checkInSummaryTask;
        var ticketTypes = await ticketTypesTask;

        // Tổng hợp số vé bán theo từng hạng
        var tierSalesMap = orderRevenue.TicketTypesSales.ToDictionary(x => x.TicketTypeId);

        var tiersBreakdown = ticketTypes.Select(tt =>
        {
            var hasSales = tierSalesMap.TryGetValue(tt.Id, out var sales);
            var sold = hasSales && sales is not null ? sales.QuantitySold : tt.SoldQuantity;
            var rev = hasSales && sales is not null ? sales.TotalRevenue : (sold * tt.Price);

            return new EventTierAnalyticsDto(
                TicketTypeId: tt.Id,
                TicketTypeName: tt.Name,
                Price: tt.Price,
                TotalCapacity: tt.TotalQuantity,
                QuantitySold: sold,
                QuantityAvailable: tt.AvailableQuantity,
                TotalRevenue: rev);
        }).ToList();

        // Nếu có hạng vé bán từ Orders mà không nằm trong ticketTypes query
        foreach (var extra in orderRevenue.TicketTypesSales)
        {
            if (tiersBreakdown.All(t => t.TicketTypeId != extra.TicketTypeId))
            {
                tiersBreakdown.Add(new EventTierAnalyticsDto(
                    TicketTypeId: extra.TicketTypeId,
                    TicketTypeName: extra.TicketTypeName,
                    Price: 0,
                    TotalCapacity: extra.QuantitySold,
                    QuantitySold: extra.QuantitySold,
                    QuantityAvailable: 0,
                    TotalRevenue: extra.TotalRevenue));
            }
        }

        var totalSold = orderRevenue.TotalTicketsSold > 0 ? orderRevenue.TotalTicketsSold : tiersBreakdown.Sum(x => x.QuantitySold);
        var totalCheckedIn = checkInSummary.TotalCheckedIn;
        var attendanceRate = totalSold > 0
            ? Math.Round(((double)totalCheckedIn / totalSold) * 100, 2)
            : 0.0;

        var response = new EventAnalyticsResponse(
            EventId: eventId,
            EventTitle: @event.Title,
            Status: @event.Status,
            TotalRevenue: orderRevenue.TotalRevenue,
            TotalOrdersCount: orderRevenue.TotalOrdersCount,
            TotalTicketsSold: totalSold,
            TotalCheckedIn: totalCheckedIn,
            AttendanceRatePercentage: attendanceRate,
            TiersBreakdown: tiersBreakdown,
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
            logger.LogWarning(ex, "Lỗi khi lưu bảng EventStatistics cho sự kiện {EventId}", eventId);
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
                logger.LogWarning(ex, "Lỗi khi lưu Redis cache báo cáo phân tích sự kiện {EventId}", eventId);
            }
        }

        return Result.Success(response);
    }
}
