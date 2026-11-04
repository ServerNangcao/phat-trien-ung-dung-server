using MediatR;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderItemDto> Items) : IRequest<Result<Guid>>;

public sealed record OrderItemDto(Guid TicketTypeId, int Quantity, decimal Price);