using MediatR;

namespace Ticketing.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Published after a refund has been approved and processed. Informs downstream modules
/// (Ticketing releases any issued tickets; Events updates revenue analytics).
/// </summary>
public sealed record RefundProcessedIntegrationEvent : IIntegrationEvent, INotification
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime RefundedAt { get; init; }

    public RefundProcessedIntegrationEvent(Guid orderId, Guid customerId, decimal totalAmount, DateTime refundedAt)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        RefundedAt = refundedAt;
    }
}
