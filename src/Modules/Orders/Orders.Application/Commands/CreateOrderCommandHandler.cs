using MediatR;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Domain.ValueObjects;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<CreateOrderResult>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketingModuleApi _ticketingModuleApi;
    private readonly IClock _clock;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork,
        ITicketingModuleApi ticketingModuleApi, IClock clock)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _ticketingModuleApi = ticketingModuleApi;
        _clock = clock;
    }

    public async Task<Result<CreateOrderResult>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var reservationResult = await _ticketingModuleApi.GetReservationAsync(request.ReservationId, cancellationToken);
        if (reservationResult.IsFailure)
            return Result<CreateOrderResult>.Failure(reservationResult.Error);

        var reservation = reservationResult.Value;

        if (reservation.CustomerId != request.CustomerId)
            return Result<CreateOrderResult>.Failure(Error.Validation("The reservation belongs to a different customer."));

        if (!reservation.IsActive || reservation.ExpiresAtUtc <= _clock.UtcNow)
            return Result<CreateOrderResult>.Failure(Error.Validation(
                "The reservation has expired or is no longer active. Please reserve the tickets again."));

        // Idempotency: one reservation yields at most one active order, so a client retry
        // after a timeout never creates a duplicate order.
        var existing = await _orderRepository.GetByReservationIdAsync(reservation.ReservationId, cancellationToken);
        if (existing is not null && existing.Status != OrderStatus.Cancelled)
            return Result<CreateOrderResult>.Success(new CreateOrderResult(existing.Id, existing.ReservationId, existing.TotalAmount));

        var items = new List<OrderItem>(reservation.Items.Count);
        foreach (var item in reservation.Items)
        {
            var orderItem = OrderItem.Create(item.TicketTypeId, item.Quantity, item.UnitPrice);
            if (orderItem.IsFailure)
                return Result<CreateOrderResult>.Failure(orderItem.Error);

            items.Add(orderItem.Value);
        }

        var total = items.Sum(i => i.Subtotal);

        var order = Order.Create(reservation.CustomerId, reservation.ReservationId, total, items, _clock.UtcNow);
        if (order.IsFailure)
            return Result<CreateOrderResult>.Failure(order.Error);

        await _orderRepository.AddAsync(order.Value, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<CreateOrderResult>.Success(new CreateOrderResult(order.Value.Id, order.Value.ReservationId, order.Value.TotalAmount));
    }
}
