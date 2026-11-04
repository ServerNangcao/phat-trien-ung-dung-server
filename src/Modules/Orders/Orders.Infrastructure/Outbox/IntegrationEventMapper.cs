using Ticketing.Modules.Orders.Contracts.IntegrationEvents;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Domain.Events;
using Ticketing.SharedKernel.Domain;

namespace Ticketing.Modules.Orders.Infrastructure.Outbox;

/// <summary>
/// Maps Orders domain events to their cross-module integration event payloads.
/// Domain events without a mapping are intra-module signals and stay out of the outbox.
/// </summary>
internal static class IntegrationEventMapper
{
    public static IIntegrationEvent? Map(IDomainEvent domainEvent, Order order)
    {
        return domainEvent switch
        {
            OrderPaid paid => new OrderPaidIntegrationEvent(
                order.Id,
                order.CustomerId,
                order.ReservationId,
                order.TotalAmount,
                paid.PaidAt,
                MapItems(order)),

            OrderPaidAfterExpiry expired => new OrderPaidAfterExpiryIntegrationEvent(
                order.Id,
                order.CustomerId,
                order.ReservationId,
                order.TotalAmount,
                expired.PaidAt,
                expired.Reason,
                MapItems(order)),

            OrderRefunded refunded => new RefundProcessedIntegrationEvent(
                order.Id,
                order.CustomerId,
                order.TotalAmount,
                refunded.RefundedAt),

            _ => null
        };
    }

    private static IReadOnlyList<OrderItemIntegrationDto> MapItems(Order order)
        => order.Items.Select(i => new OrderItemIntegrationDto(i.TicketTypeId, i.Quantity, i.Price)).ToList();
}
