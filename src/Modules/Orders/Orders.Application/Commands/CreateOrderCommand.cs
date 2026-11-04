using MediatR;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Creates an order from an active ticket reservation. Prices and quantities come from the
/// Ticketing module via the reservation, never from the client.
/// </summary>
public sealed record CreateOrderCommand(Guid CustomerId, Guid ReservationId) : IRequest<Result<CreateOrderResult>>;

public sealed record CreateOrderResult(Guid OrderId, Guid ReservationId, decimal TotalAmount);
