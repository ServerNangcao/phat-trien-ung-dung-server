using MediatR;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Customer cancels an unpaid order (FR-16 "hủy đơn hoàn vé" for the pending state). The
/// reservation release is best-effort: when it fails, the reservation's 10-minute TTL
/// returns the stock anyway, so the cancellation itself must not fail because of it.
/// </summary>
public sealed record CancelOrderCommand(Guid OrderId) : IRequest<Result<CancelOrderResult>>;

public sealed record CancelOrderResult(Guid OrderId, string Status);

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result<CancelOrderResult>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketingModuleApi _ticketingModuleApi;
    private readonly IClock _clock;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork,
        ITicketingModuleApi ticketingModuleApi, IClock clock, ILogger<CancelOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _ticketingModuleApi = ticketingModuleApi;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CancelOrderResult>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<CancelOrderResult>.Failure(Error.NotFound($"Order {request.OrderId} not found."));

        // Idempotent: if already cancelled, return success with current status
        if (order.Status == OrderStatus.Cancelled)
            return Result<CancelOrderResult>.Success(new CancelOrderResult(order.Id, order.Status.ToString()));

        if (order.Status != OrderStatus.Pending)
            return Result<CancelOrderResult>.Failure(Error.Validation(
                $"Only pending orders can be cancelled. Order {order.Id} is {order.Status}."));

        order.Cancel(_clock.UtcNow);

        var release = await _ticketingModuleApi.CancelReservationAsync(order.ReservationId, cancellationToken);
        if (release.IsFailure)
        {
            // Self-healing: the hold expires within 10 minutes even if the release failed.
            _logger.LogWarning("Reservation {ReservationId} release failed after cancelling order {OrderId}: {Error}",
                order.ReservationId, order.Id, release.Error.Message);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<CancelOrderResult>.Success(new CancelOrderResult(order.Id, order.Status.ToString()));
    }
}
