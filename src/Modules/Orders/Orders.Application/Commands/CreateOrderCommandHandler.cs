using MediatR;
using Ticketing.Infrastructure.Core.EventBus;
using Ticketing.SharedKernel;
using Ticketing.Modules.Orders.Contracts;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Domain.Events;
using Ticketing.Modules.Orders.Domain.ValueObjects;

namespace Ticketing.Modules.Orders.Application.Commands;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;
    private readonly IClock _clock;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IEventBus eventBus, IClock clock)
    {
        _orderRepository = orderRepository;
        _eventBus = eventBus;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Validate items
        var items = request.Items.Select(i => OrderItem.Create(i.TicketTypeId, i.Quantity, i.Price)).ToList();
        if (items.Any(r => r.IsFailure))
            return Result<Guid>.Failure(Error.Validation(items.First(r => r.IsFailure).Error.Message));

        var total = items.Sum(i => i.Value.Price * i.Value.Quantity);
        var order = Order.Create(request.CustomerId, total, items.Select(i => i.Value).ToList(), _clock.UtcNow);
        if (order.IsFailure)
            return Result<Guid>.Failure(order.Error);

        await _orderRepository.AddAsync(order.Value, cancellationToken);
        await _eventBus.PublishAsync(new OrderCreated(order.Value.Id, order.Value.CustomerId, total, order.Value.CreatedAt), cancellationToken);
        return Result<Guid>.Success(order.Value.Id);
    }
}