using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Events;

/// <summary>
/// Raised when payment is confirmed for an order whose reservation had already expired (ADR-0003).
/// The payment must be refunded; ticket issuance must NOT proceed for this order.
/// </summary>
public sealed record OrderPaidAfterExpiry : DomainEvent
{
    /// <summary>
    /// The identifier of the order that was paid.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// The customer who paid the order.
    /// </summary>
    public Guid CustomerId { get; init; }

    /// <summary>
    /// The total amount that was paid.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// The date and time the payment was confirmed.
    /// </summary>
    public DateTime PaidAt { get; init; }

    /// <summary>
    /// Why the payment is being refunded.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    public OrderPaidAfterExpiry(Guid orderId, Guid customerId, decimal totalAmount, DateTime paidAt, string reason)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        PaidAt = paidAt;
        Reason = reason;
    }
}
