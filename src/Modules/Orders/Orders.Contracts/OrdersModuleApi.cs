using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Contracts;

/// <summary>
/// Public synchronous API of the Orders module (TV2), consumed by other modules through
/// their Contracts reference. Backs FR-23: the Events module's revenue dashboard calls
/// <see cref="GetSalesSummaryAsync"/> instead of querying the orders schema directly.
/// The host (TV1) binds this interface to the in-process implementation registered by
/// AddOrdersModule; a separate host could bind an HTTP adapter without changing consumers.
/// </summary>
public interface IOrdersModuleApi
{
    /// <summary>
    /// Revenue summary for one event: total revenue, paid order count and the per-ticket-type
    /// breakdown. Revenue counts orders in the Paid state only — Refunded and
    /// PaidAfterExpiry (ADR-0003) money is returned to the customer, not earned.
    /// </summary>
    Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Aggregated sales for one event (FR-23).</summary>
public sealed record SalesSummaryDto(
    Guid EventId,
    decimal TotalRevenue,
    int PaidOrderCount,
    IReadOnlyList<SalesByTicketTypeDto> ByTicketType);

/// <summary>Revenue and volume contributed by one ticket type.</summary>
public sealed record SalesByTicketTypeDto(
    Guid TicketTypeId,
    string TicketTypeName,
    int TicketsSold,
    decimal Revenue);
