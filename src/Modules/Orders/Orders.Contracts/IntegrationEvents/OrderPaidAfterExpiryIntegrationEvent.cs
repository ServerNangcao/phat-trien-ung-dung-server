using MediatR;

namespace Ticketing.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Payment was confirmed after the underlying reservation expired (ADR-0003).
/// The amount must be refunded; ticket issuance must NOT proceed for this order.
/// </summary>
public sealed record OrderPaidAfterExpiryIntegrationEvent : IIntegrationEvent, INotification
{
    /// <summary>Unique per occurrence; consumers' Inbox pattern dedupes redeliveries by it.</summary>
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid ReservationId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime PaidAt { get; init; }
    public string Reason { get; init; } = string.Empty;

    /// <summary>Customer contact email for the refund notification; empty when unknown.</summary>
    public string CustomerEmail { get; init; } = string.Empty;

    public IReadOnlyList<OrderItemIntegrationDto> Items { get; init; } = new List<OrderItemIntegrationDto>();

    public OrderPaidAfterExpiryIntegrationEvent(Guid eventId, Guid orderId, Guid customerId, Guid reservationId,
        decimal totalAmount, DateTime paidAt, string reason, string customerEmail, IReadOnlyList<OrderItemIntegrationDto> items)
    {
        EventId = eventId;
        OrderId = orderId;
        CustomerId = customerId;
        ReservationId = reservationId;
        TotalAmount = totalAmount;
        PaidAt = paidAt;
        Reason = reason;
        CustomerEmail = customerEmail;
        Items = items;
    }
}
