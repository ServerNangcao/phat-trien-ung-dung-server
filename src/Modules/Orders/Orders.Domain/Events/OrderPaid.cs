using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Events;

/// <summary>
/// Raised when an order's payment is confirmed.
/// </summary>
public sealed record OrderPaid : DomainEvent
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

    public OrderPaid(Guid orderId, Guid customerId, decimal totalAmount, DateTime paidAt)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        PaidAt = paidAt;
    }
}
