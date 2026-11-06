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
    private readonly IDiscountCodeRepository _discountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketingModuleApi _ticketingModuleApi;
    private readonly IClock _clock;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IDiscountCodeRepository discountRepository,
        IUnitOfWork unitOfWork, ITicketingModuleApi ticketingModuleApi, IClock clock)
    {
        _orderRepository = orderRepository;
        _discountRepository = discountRepository;
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
            return Result<CreateOrderResult>.Success(new CreateOrderResult(existing.Id, existing.ReservationId, existing.TotalAmount, existing.DiscountAmount));

        var items = new List<OrderItem>(reservation.Items.Count);
        foreach (var item in reservation.Items)
        {
            var orderItem = OrderItem.Create(item.TicketTypeId, item.Quantity, item.UnitPrice, item.TicketTypeName);
            if (orderItem.IsFailure)
                return Result<CreateOrderResult>.Failure(orderItem.Error);

            items.Add(orderItem.Value);
        }

        var itemsTotal = items.Sum(i => i.Subtotal);
        var discountAmount = 0m;
        DiscountCode? discount = null;

        // FR-16: a discount code is validated server-side; an invalid one fails the
        // request instead of silently charging full price.
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            discount = await _discountRepository.GetByCodeAsync(request.DiscountCode, cancellationToken);
            if (discount is null)
                return Result<CreateOrderResult>.Failure(Error.Validation($"Discount code '{request.DiscountCode}' does not exist."));

            var discountResult = discount.ComputeDiscount(itemsTotal, _clock.UtcNow);
            if (discountResult.IsFailure)
                return Result<CreateOrderResult>.Failure(discountResult.Error);

            discountAmount = discountResult.Value;
        }

        var total = itemsTotal - discountAmount;

        var order = Order.Create(reservation.CustomerId, reservation.ReservationId, reservation.EventId, total, items, _clock.UtcNow,
            reservation.CustomerEmail, discountAmount);
        if (order.IsFailure)
            return Result<CreateOrderResult>.Failure(order.Error);

        await _orderRepository.AddAsync(order.Value, cancellationToken);

        // The redemption count commits in the same transaction as the order, so a rolled
        // back order never consumes a redemption.
        discount?.RecordRedemption();

        var commit = await _unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            // A concurrent create on the same reservation won the unique index; return its
            // order instead of surfacing the race as a 500.
            var winner = await _orderRepository.GetByReservationIdAsync(reservation.ReservationId, cancellationToken);
            if (winner is not null)
                return Result<CreateOrderResult>.Success(new CreateOrderResult(winner.Id, winner.ReservationId, winner.TotalAmount, winner.DiscountAmount));

            return Result<CreateOrderResult>.Failure(commit.Error);
        }

        return Result<CreateOrderResult>.Success(new CreateOrderResult(order.Value.Id, order.Value.ReservationId, order.Value.TotalAmount, discountAmount));
    }
}
