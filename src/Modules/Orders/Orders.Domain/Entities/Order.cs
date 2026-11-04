using Ticketing.Modules.Orders.Domain.Events;
using Ticketing.Modules.Orders.Domain.ValueObjects;
using Ticketing.SharedKernel;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Entities;

/// <summary>
/// Aggregate root representing a customer order.
/// An order is created from a ticket reservation and tracks payment and refund state.
/// </summary>
public sealed class Order : Entity
{
    /// <summary>
    /// The customer who placed this order.
    /// </summary>
    public Guid CustomerId { get; private init; }

    /// <summary>
    /// The reservation this order was created from. Unique: one reservation yields at most one active order.
    /// </summary>
    public Guid ReservationId { get; private init; }

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
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>
    /// The date and time the order was created.
    /// </summary>
    public DateTime CreatedAt { get; private init; }

    /// <summary>
    /// The date and time the order was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Pending domain events raised by this aggregate. Cleared after each save.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    private readonly List<OrderItem> _items = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    private Order() : base(Guid.Empty)
    {
    }

    private Order(Guid id, Guid customerId, Guid reservationId, decimal totalAmount, DateTime createdAt)
        : base(id)
    {
        CustomerId = customerId;
        ReservationId = reservationId;
        TotalAmount = totalAmount;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>
    /// Create a new order, validating the input values.
    /// </summary>
    /// <param name="customerId">The customer placing the order.</param>
    /// <param name="reservationId">The ticket reservation this order fulfills.</param>
    /// <param name="totalAmount">The computed total across all line items.</param>
    /// <param name="items">The line items in the order.</param>
    /// <param name="now">The current UTC timestamp.</param>
    /// <returns>A result containing the order on success, or a validation error.</returns>
    public static Result<Order> Create(Guid customerId, Guid reservationId, decimal totalAmount,
        IReadOnlyList<OrderItem> items, DateTime now)
    {
        if (customerId == Guid.Empty)
            return Result<Order>.Failure(Error.Validation("Customer identifier cannot be empty."));

        if (reservationId == Guid.Empty)
            return Result<Order>.Failure(Error.Validation("Reservation identifier cannot be empty."));

        if (items is null || items.Count == 0)
            return Result<Order>.Failure(Error.Validation("Order must contain at least one item."));

        var itemsTotal = items.Sum(i => i.Subtotal);
        if (totalAmount != itemsTotal)
            return Result<Order>.Failure(Error.Validation(
                $"Total amount {totalAmount} does not match the sum of item subtotals {itemsTotal}."));

        var order = new Order(Guid.NewGuid(), customerId, reservationId, totalAmount, now);
        order._items.AddRange(items);

        order._domainEvents.Add(new OrderCreated(order.Id, order.CustomerId, order.TotalAmount, order.CreatedAt));

        return Result<Order>.Success(order);
    }

    /// <summary>
    /// Mark this order as paid. Valid only from Pending.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkAsPaid(DateTime now)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Order {Id} cannot transition from {Status} to Paid.");

        Status = OrderStatus.Paid;
        UpdatedAt = now;
        _domainEvents.Add(new OrderPaid(Id, CustomerId, TotalAmount, now));
    }

    /// <summary>
    /// Payment arrived after the reservation expired (ADR-0003). The order is paid but must be refunded,
    /// and ticket issuance must not proceed.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    /// <param name="reason">Why the payment is being refunded.</param>
    public void MarkAsPaidAfterExpiry(DateTime now, string reason)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Order {Id} cannot transition from {Status} to PaidAfterExpiry.");

        Status = OrderStatus.PaidAfterExpiry;
        UpdatedAt = now;
        _domainEvents.Add(new OrderPaidAfterExpiry(Id, CustomerId, TotalAmount, now, reason));
    }

    /// <summary>
    /// Request a refund for this order. Valid only from Paid.
    /// </summary>
    /// <param name="reason">The reason for the refund request.</param>
    /// <param name="now">The current UTC timestamp.</param>
    public void RequestRefund(string reason, DateTime now)
    {
        if (Status != OrderStatus.Paid)
            throw new InvalidOperationException($"Only paid orders can request a refund. Order {Id} is {Status}.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Refund reason cannot be empty.", nameof(reason));

        Status = OrderStatus.RefundRequested;
        UpdatedAt = now;
        _domainEvents.Add(new OrderRefundRequested(Id, reason, now));
    }

    /// <summary>
    /// Mark the refund as fully processed. Valid from RefundRequested (customer-initiated)
    /// or PaidAfterExpiry (ADR-0003 auto-refund).
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkAsRefunded(DateTime now)
    {
        if (Status is not (OrderStatus.RefundRequested or OrderStatus.PaidAfterExpiry))
            throw new InvalidOperationException($"Order {Id} cannot be refunded from status {Status}.");

        Status = OrderStatus.Refunded;
        UpdatedAt = now;
        _domainEvents.Add(new OrderRefunded(Id, now));
    }

    /// <summary>
    /// A refund request was rejected; the order returns to Paid.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkRefundRejected(DateTime now)
    {
        if (Status != OrderStatus.RefundRequested)
            throw new InvalidOperationException($"Order {Id} cannot reject a refund from status {Status}.");

        Status = OrderStatus.Paid;
        UpdatedAt = now;
    }

    /// <summary>
    /// Cancel this order. Valid only from Pending.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void Cancel(DateTime now)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Only pending orders can be cancelled. Order {Id} is {Status}.");

        Status = OrderStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>
    /// Retrieve and clear all pending domain events from this aggregate.
    /// </summary>
    public IReadOnlyList<IDomainEvent> GetAndClearDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
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

    /// <summary>
    /// Payment arrived after the reservation expired; the order must be refunded (ADR-0003).
    /// </summary>
    PaidAfterExpiry = 5
}
