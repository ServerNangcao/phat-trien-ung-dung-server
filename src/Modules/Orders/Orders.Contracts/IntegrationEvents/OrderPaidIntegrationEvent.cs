using MediatR;

namespace Ticketing.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Integration event published when an order is paid. The Ticketing module (TV4) issues
/// tickets from it. Delivered via the Outbox for at-least-once processing.
/// </summary>
public sealed record OrderPaidIntegrationEvent : IIntegrationEvent, INotification
{
    /// <summary>Unique per occurrence; consumers' Inbox pattern dedupes redeliveries by it.</summary>
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid ReservationId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime PaidAt { get; init; }
    public IReadOnlyList<OrderItemIntegrationDto> Items { get; init; } = new List<OrderItemIntegrationDto>();

    public OrderPaidIntegrationEvent(Guid eventId, Guid orderId, Guid customerId, Guid reservationId, decimal totalAmount,
        DateTime paidAt, IReadOnlyList<OrderItemIntegrationDto> items)
    {
        EventId = eventId;
        OrderId = orderId;
        CustomerId = customerId;
        ReservationId = reservationId;
        TotalAmount = totalAmount;
        PaidAt = paidAt;
        Items = items;
    }
}

public sealed record OrderItemIntegrationDto(Guid TicketTypeId, int Quantity, decimal Price, string TicketTypeName = "");

/// <summary>
/// Marker interface for integration events crossing module boundaries.
/// </summary>
public interface IIntegrationEvent;
