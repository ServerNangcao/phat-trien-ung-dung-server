using MediatR;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Queries;

/// <summary>
/// FR-13: Customer order list.
/// </summary>
public sealed record GetCustomerOrdersQuery(Guid CustomerId) : IRequest<Result<IReadOnlyList<OrderSummaryDto>>>;

public sealed record OrderSummaryDto(Guid OrderId, Guid ReservationId, decimal TotalAmount, string Status, DateTime CreatedAt);

public sealed class GetCustomerOrdersQueryHandler : IRequestHandler<GetCustomerOrdersQuery, Result<IReadOnlyList<OrderSummaryDto>>>
{
    private readonly IOrderRepository _orderRepository;

    public GetCustomerOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<IReadOnlyList<OrderSummaryDto>>> Handle(GetCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.ListByCustomerAsync(request.CustomerId, cancellationToken);
        var dtos = orders.Select(o => new OrderSummaryDto(o.Id, o.ReservationId, o.TotalAmount, o.Status.ToString(), o.CreatedAt)).ToList();
        return Result<IReadOnlyList<OrderSummaryDto>>.Success(dtos);
    }
}

/// <summary>
/// FR-13: Order detail (items).
/// </summary>
public sealed record GetOrderDetailQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;

public sealed record OrderDetailDto(
    Guid OrderId,
    Guid ReservationId,
    Guid CustomerId,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(Guid TicketTypeId, int Quantity, decimal Price, decimal Subtotal);

public sealed class GetOrderDetailQueryHandler : IRequestHandler<GetOrderDetailQuery, Result<OrderDetailDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderDetailQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<OrderDetailDto>> Handle(GetOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDetailDto>.Failure(Error.NotFound($"Order {request.OrderId} not found."));

        var dto = new OrderDetailDto(
            order.Id,
            order.ReservationId,
            order.CustomerId,
            order.TotalAmount,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new OrderItemDto(i.TicketTypeId, i.Quantity, i.Price, i.Subtotal)).ToList());

        return Result<OrderDetailDto>.Success(dto);
    }
}

/// <summary>
/// FR-22: Organizer attendee list for an event.
/// </summary>
public sealed record GetEventAttendeesQuery(Guid EventId) : IRequest<Result<IReadOnlyList<AttendeeDto>>>;

public sealed record AttendeeDto(Guid TicketId, Guid OrderId, Guid CustomerId, string TicketType, string Status, DateTime IssuedAt);

public sealed class GetEventAttendeesQueryHandler : IRequestHandler<GetEventAttendeesQuery, Result<IReadOnlyList<AttendeeDto>>>
{
    private readonly ITicketingModuleApi _ticketingModuleApi;

    public GetEventAttendeesQueryHandler(ITicketingModuleApi ticketingModuleApi)
    {
        _ticketingModuleApi = ticketingModuleApi;
    }

    public async Task<Result<IReadOnlyList<AttendeeDto>>> Handle(GetEventAttendeesQuery request, CancellationToken cancellationToken)
    {
        var result = await _ticketingModuleApi.GetTicketsByEventAsync(request.EventId, cancellationToken);
        if (result.IsFailure)
            return Result<IReadOnlyList<AttendeeDto>>.Failure(result.Error);

        var dtos = result.Value.Select(t => new AttendeeDto(
            t.TicketId,
            t.OrderId,
            t.CustomerId,
            t.TicketTypeName,
            t.Status,
            t.IssuedAtUtc)).ToList();

        return Result<IReadOnlyList<AttendeeDto>>.Success(dtos);
    }
}