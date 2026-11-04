using MediatR;

namespace Ticketing.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Payment was confirmed after the underlying reservation expired (ADR-0003).
/// The amount must be refunded; ticket issuance must NOT proceed for this order.
/// </summary>
public sealed record OrderPaidAfterExpiryIntegrationEvent : IIntegrationEvent, INotification
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid ReservationId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime PaidAt { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<OrderItemIntegrationDto> Items { get; init; } = new List<OrderItemIntegrationDto>();

    public OrderPaidAfterExpiryIntegrationEvent(Guid orderId, Guid customerId, Guid reservationId,
        decimal totalAmount, DateTime paidAt, string reason, IReadOnlyList<OrderItemIntegrationDto> items)
    {
        OrderId = orderId;
        CustomerId = customerId;
        ReservationId = reservationId;
        TotalAmount = totalAmount;
        PaidAt = paidAt;
        Reason = reason;
        Items = items;
    }
}
