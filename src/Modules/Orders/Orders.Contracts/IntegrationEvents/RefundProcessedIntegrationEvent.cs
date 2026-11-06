using MediatR;

namespace Ticketing.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Published after a refund has been approved and processed. Informs downstream modules
/// (Ticketing releases any issued tickets; Events updates revenue analytics; Notifications
/// emails the customer).
/// </summary>
public sealed record RefundProcessedIntegrationEvent : IIntegrationEvent, INotification
{
    /// <summary>Unique per occurrence; consumers' Inbox pattern dedupes redeliveries by it.</summary>
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime RefundedAt { get; init; }

    /// <summary>Customer contact email for the refund notification; empty when unknown.</summary>
    public string CustomerEmail { get; init; } = string.Empty;

    public RefundProcessedIntegrationEvent(Guid eventId, Guid orderId, Guid customerId, decimal totalAmount,
        DateTime refundedAt, string customerEmail = "")
    {
        EventId = eventId;
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        RefundedAt = refundedAt;
        CustomerEmail = customerEmail;
    }
}
