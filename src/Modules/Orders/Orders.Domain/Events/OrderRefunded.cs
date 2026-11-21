using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Events;

/// <summary>
/// Raised when an order's refund has been processed and completed.
/// </summary>
public sealed record OrderRefunded : DomainEvent
{
    /// <summary>
    /// The identifier of the order that was refunded.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// The date and time the refund was processed.
    /// </summary>
    public DateTime RefundedAt { get; init; }

    public OrderRefunded(Guid orderId, DateTime refundedAt)
    {
        OrderId = orderId;
        RefundedAt = refundedAt;
    }
}
