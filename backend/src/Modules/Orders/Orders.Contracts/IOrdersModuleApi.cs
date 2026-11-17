namespace Orders.Contracts;

public sealed record TicketTypeSalesDto(
    Guid TicketTypeId,
    string TicketTypeName,
    int QuantitySold,
    decimal TotalRevenue);

public sealed record EventRevenueSummaryDto(
    Guid EventId,
    decimal TotalRevenue,
    int TotalOrdersCount,
    int TotalTicketsSold,
    IReadOnlyList<TicketTypeSalesDto> TicketTypesSales);

/// <summary>
/// API công khai của module Orders.
/// Các module khác (như Events/Analytics) gọi qua interface này (in-process).
/// </summary>
public interface IOrdersModuleApi
{
    /// <summary>
    /// Lấy báo cáo chi tiết doanh thu và vé bán theo từng hạng vé của một sự kiện.
    /// </summary>
    Task<EventRevenueSummaryDto> GetEventRevenueSummaryAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tổng doanh thu của nhiều sự kiện (dùng cho Organizer dashboard list).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetTotalRevenuesByEventIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default);
}
