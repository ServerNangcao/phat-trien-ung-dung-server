using Ticketing.Modules.Orders.Domain.ValueObjects;
using Ticketing.Modules.Orders.Domain.Events;
using Ticketing.SharedKernel;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Entities;

/// <summary>
/// Aggregate root representing a customer order.
/// An order contains one or more line items and tracks payment and refund state.
/// </summary>
public sealed class Order : Entity
{
    /// <summary>
    /// The customer who placed this order.
    /// </summary>
    public Guid CustomerId { get; private init; }

    /// <summary>
    /// The total monetary value of this order, computed from its line items.
    /// </summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>
    /// The current lifecycle state of the order.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// The line items that compose this order.
    /// </summary>
    public IReadOnlyList<OrderItem> Items { get; private init; }

    /// <summary>
    /// The date and time the order was created.
    /// </summary>
    public DateTime CreatedAt { get; private init; }

    /// <summary>
    /// The date and time the order was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Pending domain events raised by this aggregate.
    /// </summary>
    public List<IDomainEvent> DomainEvents { get; private set; }

    private Order(Guid id, Guid customerId, decimal totalAmount, OrderStatus status,
        IReadOnlyList<OrderItem> items, DateTime createdAt)
        : base(id)
    {
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Status = status;
        Items = items;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        DomainEvents = new List<IDomainEvent>();
    }

    /// <summary>
    /// Create a new order, validating the input values.
    /// </summary>
    /// <param name="customerId">The customer placing the order.</param>
    /// <param name="totalAmount">The computed total across all line items.</param>
    /// <param name="items">The line items in the order.</param>
    /// <param name="now">The current UTC timestamp.</param>
    /// <returns>A result containing the order on success, or a validation error.</returns>
    public static Result<Order> Create(Guid customerId, decimal totalAmount,
        IReadOnlyList<OrderItem> items, DateTime now)
    {
        if (customerId == Guid.Empty)
            return Result<Order>.Failure(Error.Validation("Customer identifier cannot be empty."));

        if (items is null || items.Count == 0)
            return Result<Order>.Failure(Error.Validation("Order must contain at least one item."));

        if (totalAmount < 0)
            return Result<Order>.Failure(Error.Validation("Total amount cannot be negative."));

        var order = new Order(
            id: Guid.NewGuid(),
            customerId: customerId,
            totalAmount: totalAmount,
            status: OrderStatus.Pending,
            items: items,
            createdAt: now);

        order.DomainEvents.Add(new OrderCreated(order.Id, order.CustomerId, order.TotalAmount, order.CreatedAt));

        return Result<Order>.Success(order);
    }

    /// <summary>
    /// Mark this order as paid.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkAsPaid(DateTime now)
    {
        Status = OrderStatus.Paid;
        UpdatedAt = now;
        DomainEvents.Add(new OrderPaid(Id, CustomerId, TotalAmount, now));
    }

    /// <summary>
    /// Request a refund for this order.
    /// </summary>
    /// <param name="reason">The reason for the refund request.</param>
    /// <param name="now">The current UTC timestamp.</param>
    public void RequestRefund(string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Refund reason cannot be empty.", nameof(reason));

        Status = OrderStatus.RefundRequested;
        UpdatedAt = now;
        DomainEvents.Add(new OrderRefundRequested(Id, reason, now));
    }

    /// <summary>
    /// Mark the refund as fully processed.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkAsRefunded(DateTime now)
    {
        Status = OrderStatus.Refunded;
        UpdatedAt = now;
    }

    /// <summary>
    /// Cancel this order.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void Cancel(DateTime now)
    {
        Status = OrderStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>
    /// Retrieve and clear all pending domain events from this aggregate.
    /// </summary>
    public IReadOnlyList<IDomainEvent> GetAndClearDomainEvents()
    {
        var events = DomainEvents.ToArray();
        DomainEvents.Clear();
        return events;
    }
}

/// <summary>
/// The current lifecycle state of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order has been created but payment has not been received.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Payment has been confirmed.
    /// </summary>
    Paid = 1,

    /// <summary>
    /// A refund has been requested for this order.
    /// </summary>
    RefundRequested = 2,

    /// <summary>
    /// The refund has been processed and completed.
    /// </summary>
    Refunded = 3,

    /// <summary>
    /// The order has been cancelled.
    /// </summary>
    Cancelled = 4,
}
