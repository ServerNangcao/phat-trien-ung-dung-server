using MediatR;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Creates an order from an active ticket reservation. Prices and quantities come from the
/// Ticketing module via the reservation, never from the client. An optional discount code
/// (FR-16) is validated server-side and reduces the payable total.
/// </summary>
public sealed record CreateOrderCommand(Guid CustomerId, Guid ReservationId, string? DiscountCode = null)
    : IRequest<Result<CreateOrderResult>>;

public sealed record CreateOrderResult(Guid OrderId, Guid ReservationId, decimal TotalAmount, decimal DiscountAmount);
