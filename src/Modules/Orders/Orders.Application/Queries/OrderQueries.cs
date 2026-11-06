using MediatR;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Contracts;
using Ticketing.Modules.Orders.Domain.Entities;
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
/// FR-23 / PDF 3.3(e): revenue summary for one event, consumed by the Events module's
/// dashboard through IOrdersModuleApi.GetSalesSummaryAsync. Revenue counts Paid orders
/// only — refunded money is returned to the customer, not earned.
/// </summary>
public sealed record GetEventSalesSummaryQuery(Guid EventId) : IRequest<Result<SalesSummaryDto>>;

public sealed class GetEventSalesSummaryQueryHandler : IRequestHandler<GetEventSalesSummaryQuery, Result<SalesSummaryDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetEventSalesSummaryQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<SalesSummaryDto>> Handle(GetEventSalesSummaryQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.ListByEventAsync(request.EventId, cancellationToken);

        var paidOrders = orders.Where(o => o.Status == OrderStatus.Paid).ToList();
        var byTicketType = paidOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.TicketTypeId)
            .Select(g => new SalesByTicketTypeDto(
                g.Key,
                g.First().TicketTypeName,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Subtotal)))
            .OrderByDescending(t => t.Revenue)
            .ToList();

        var summary = new SalesSummaryDto(
            request.EventId,
            paidOrders.Sum(o => o.TotalAmount),
            paidOrders.Count,
            byTicketType);

        return Result<SalesSummaryDto>.Success(summary);
    }
}

/// <summary>
/// FR-22: full order list for one event (CSV export feed). Includes every order state so
/// the organizer sees pending and cancelled orders alongside paid ones.
/// </summary>
public sealed record GetEventOrdersQuery(Guid EventId) : IRequest<Result<IReadOnlyList<EventOrderRowDto>>>;

public sealed record EventOrderRowDto(Guid OrderId, Guid CustomerId, Guid ReservationId, decimal TotalAmount,
    string Status, DateTime CreatedAt, IReadOnlyList<EventOrderItemRowDto> Items);

public sealed record EventOrderItemRowDto(Guid TicketTypeId, string TicketTypeName, int Quantity, decimal Price);

public sealed class GetEventOrdersQueryHandler : IRequestHandler<GetEventOrdersQuery, Result<IReadOnlyList<EventOrderRowDto>>>
{
    private readonly IOrderRepository _orderRepository;

    public GetEventOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<IReadOnlyList<EventOrderRowDto>>> Handle(GetEventOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.ListByEventAsync(request.EventId, cancellationToken);
        var rows = orders.Select(o => new EventOrderRowDto(
            o.Id, o.CustomerId, o.ReservationId, o.TotalAmount, o.Status.ToString(), o.CreatedAt,
            o.Items.Select(i => new EventOrderItemRowDto(i.TicketTypeId, i.TicketTypeName, i.Quantity, i.Price)).ToList()))
            .ToList();

        return Result<IReadOnlyList<EventOrderRowDto>>.Success(rows);
    }
}

/// <summary>
/// FR-22: Organizer attendee list for an event.
/// </summary>
public sealed record GetEventAttendeesQuery(Guid EventId) : IRequest<Result<IReadOnlyList<AttendeeDto>>>;

public sealed record AttendeeDto(Guid TicketId, Guid OrderId, Guid CustomerId, Guid TicketTypeId,
    string TicketType, string Status, DateTime IssuedAt);

public sealed class GetEventAttendeesQueryHandler : IRequestHandler<GetEventAttendeesQuery, Result<IReadOnlyList<AttendeeDto>>>
{
    private readonly ITicketingModuleApi _ticketingModuleApi;
    private readonly IOrderRepository _orderRepository;

    public GetEventAttendeesQueryHandler(ITicketingModuleApi ticketingModuleApi, IOrderRepository orderRepository)
    {
        _ticketingModuleApi = ticketingModuleApi;
        _orderRepository = orderRepository;
    }

    public async Task<Result<IReadOnlyList<AttendeeDto>>> Handle(GetEventAttendeesQuery request, CancellationToken cancellationToken)
    {
        var ticketsResult = await _ticketingModuleApi.GetTicketsByEventAsync(request.EventId, cancellationToken);
        if (ticketsResult.IsFailure)
            return Result<IReadOnlyList<AttendeeDto>>.Failure(ticketsResult.Error);

        // Join Ticketing's issued tickets with Orders' payment state so the attendee list
        // only contains tickets of orders that were actually paid.
        var tickets = ticketsResult.Value;
        var orderIds = tickets.Select(t => t.OrderId).Distinct().ToList();
        var orders = await _orderRepository.ListByIdsAsync(orderIds, cancellationToken);
        var paidOrderIds = orders
            .Where(o => o.Status == OrderStatus.Paid)
            .Select(o => o.Id)
            .ToHashSet();

        var dtos = tickets
            .Where(t => paidOrderIds.Contains(t.OrderId))
            .Select(t => new AttendeeDto(t.TicketId, t.OrderId, t.CustomerId,
                t.TicketTypeId, t.TicketTypeName, t.Status, t.IssuedAtUtc))
            .ToList();

        return Result<IReadOnlyList<AttendeeDto>>.Success(dtos);
    }
}