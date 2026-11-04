using System;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Domain.Entities;

/// <summary>
/// Entity representing a refund request tied to an order.
/// </summary>
public sealed class RefundRequest : Entity
{
    /// <summary>
    /// The order this refund request belongs to.
    /// </summary>
    public Guid OrderId { get; private init; }

    /// <summary>
    /// The reason provided for the refund.
    /// </summary>
    public string Reason { get; private init; }

    /// <summary>
    /// The date and time the refund request was created.
    /// </summary>
    public DateTime CreatedAt { get; private init; }

    /// <summary>
    /// The date and time the refund was processed, if it has been completed.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Current status of the refund request.
    /// </summary>
    public RefundStatus Status { get; private set; }

    private RefundRequest(Guid id, Guid orderId, string reason, DateTime createdAt)
        : base(id)
    {
        OrderId = orderId;
        Reason = reason;
        CreatedAt = createdAt;
        ProcessedAt = null;
        Status = RefundStatus.Pending;
    }

    /// <summary>
    /// Create a new refund request.
    /// </summary>
    /// <param name="orderId">The order to refund.</param>
    /// <param name="reason">The reason for the refund.</param>
    /// <param name="now">The current UTC timestamp.</param>
    /// <returns>A new refund request instance.</returns>
    public static RefundRequest Create(Guid orderId, string reason, DateTime now)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order identifier cannot be empty.", nameof(orderId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Refund reason cannot be empty.", nameof(reason));

        return new RefundRequest(Guid.NewGuid(), orderId, reason, now);
    }

    /// <summary>
    /// Mark this refund request as processed.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkAsProcessed(DateTime now)
    {
        ProcessedAt = now;
        Status = RefundStatus.Processed;
    }

    /// <summary>
    /// Mark this refund request as rejected.
    /// </summary>
    /// <param name="now">The current UTC timestamp.</param>
    public void MarkAsRejected(DateTime now)
    {
        ProcessedAt = now;
        Status = RefundStatus.Rejected;
    }
}

/// <summary>
/// Represents the lifecycle status of a refund request.
/// </summary>
public enum RefundStatus
{
    /// <summary>Not yet processed.</summary>
    Pending,

    /// <summary>Successfully processed.</summary>
    Processed,

    /// <summary>Rejected.</summary>
    Rejected
}
