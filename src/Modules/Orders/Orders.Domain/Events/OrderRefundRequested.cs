using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Events;

/// <summary>
/// Raised when a refund is requested for an order.
/// </summary>
public sealed record OrderRefundRequested : DomainEvent
{
    /// <summary>
    /// The identifier of the order being refunded.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// The reason provided for the refund.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// The date and time the refund was requested.
    /// </summary>
    public DateTime RequestedAt { get; init; }

    public OrderRefundRequested(Guid orderId, string reason, DateTime requestedAt)
    {
        OrderId = orderId;
        Reason = reason;
        RequestedAt = requestedAt;
    }
}
