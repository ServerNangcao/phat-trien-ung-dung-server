using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Events;

/// <summary>
/// Raised when a new order is created.
/// </summary>
public sealed record OrderCreated : DomainEvent
{
    /// <summary>
    /// The identifier of the order that was created.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// The customer who placed the order.
    /// </summary>
    public Guid CustomerId { get; init; }

    /// <summary>
    /// The total monetary value of the order.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// The date and time the order was created.
    /// </summary>
    public DateTime OrderCreatedAt { get; init; }

    public OrderCreated(Guid orderId, Guid customerId, decimal totalAmount, DateTime orderCreatedAt)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        OrderCreatedAt = orderCreatedAt;
    }
}
