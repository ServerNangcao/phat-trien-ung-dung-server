using Microsoft.EntityFrameworkCore;
using Orders.Contracts;
using Orders.Internal.Domain;
using Orders.Internal.Infrastructure;

namespace Orders.Internal.Api;

internal sealed class OrdersModuleApi(OrdersDbContext db) : IOrdersModuleApi
{
    public async Task<EventRevenueSummaryDto> GetEventRevenueSummaryAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var paidOrdersQuery = db.Orders
            .Where(o => o.EventId == eventId && o.Status == OrderStatus.Paid);

        var totalRevenue = await paidOrdersQuery
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var totalOrdersCount = await paidOrdersQuery
            .CountAsync(cancellationToken);

        var tierSales = await paidOrdersQuery
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.TicketTypeId, i.TicketTypeName })
            .Select(g => new TicketTypeSalesDto(
                g.Key.TicketTypeId,
                g.Key.TicketTypeName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.UnitPrice * x.Quantity)))
            .ToListAsync(cancellationToken);

        var totalTicketsSold = tierSales.Sum(x => x.QuantitySold);

        return new EventRevenueSummaryDto(
            EventId: eventId,
            TotalRevenue: totalRevenue,
            TotalOrdersCount: totalOrdersCount,
            TotalTicketsSold: totalTicketsSold,
            TicketTypesSales: tierSales);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetTotalRevenuesByEventIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default)
    {
        var idList = eventIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, decimal>();

        var revenues = await db.Orders
            .Where(o => idList.Contains(o.EventId) && o.Status == OrderStatus.Paid)
            .GroupBy(o => o.EventId)
            .Select(g => new { EventId = g.Key, TotalRevenue = g.Sum(o => o.TotalAmount) })
            .ToDictionaryAsync(x => x.EventId, x => x.TotalRevenue, cancellationToken);

        return revenues;
    }
}
