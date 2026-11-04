using MediatR;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Commits a VNPay IPN payment confirmation (server-to-server). The callback payload is
/// parsed and signature-verified by the API layer; this command carries only the validated data.
/// </summary>
public sealed record HandleVnPayIpnCommand(Guid TransactionId, decimal Amount) : IRequest<Result<VnPayIpnResult>>;

public sealed record VnPayIpnResult(Guid OrderId, string Status);

public sealed class HandleVnPayIpnCommandHandler : IRequestHandler<HandleVnPayIpnCommand, Result<VnPayIpnResult>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketingModuleApi _ticketingModuleApi;
    private readonly IClock _clock;

    public HandleVnPayIpnCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork,
        ITicketingModuleApi ticketingModuleApi, IClock clock)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _ticketingModuleApi = ticketingModuleApi;
        _clock = clock;
    }

    public async Task<Result<VnPayIpnResult>> Handle(HandleVnPayIpnCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (order is null)
            return Result<VnPayIpnResult>.Failure(Error.NotFound($"Order {request.TransactionId} not found."));

        // Idempotency: VNPay retries IPN until acknowledged; replays must never re-process.
        if (order.Status is OrderStatus.Paid or OrderStatus.PaidAfterExpiry
            or OrderStatus.Refunded or OrderStatus.RefundRequested or OrderStatus.Cancelled)
        {
            return Result<VnPayIpnResult>.Success(new VnPayIpnResult(order.Id, order.Status.ToString()));
        }

        if (order.TotalAmount != request.Amount)
            return Result<VnPayIpnResult>.Failure(Error.Validation(
                $"Amount mismatch: order {order.TotalAmount} vs paid {request.Amount}."));

        // ADR-0003: ask the Ticketing module whether the reservation is still alive. If the
        // hold expired (and stock was released), the money arrived but tickets can no longer
        // be issued: mark PaidAfterExpiry and auto-create the refund request.
        var reservationResult = await _ticketingModuleApi.GetReservationAsync(order.ReservationId, cancellationToken);
        if (reservationResult.IsFailure)
        {
            // Transient failure: do not guess. Return failure so VNPay retries the IPN later.
            return Result<VnPayIpnResult>.Failure(reservationResult.Error);
        }

        var reservation = reservationResult.Value;
        var now = _clock.UtcNow;

        if (reservation.IsActive && reservation.ExpiresAtUtc > now)
        {
            order.MarkAsPaid(now);
        }
        else
        {
            order.MarkAsPaidAfterExpiry(now, "Payment received after the reservation expired (ADR-0003).");
            var lateRefund = RefundRequest.Create(order.Id, "Late payment after reservation expiry (ADR-0003)", now);
            await _orderRepository.AddRefundRequestAsync(lateRefund, cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<VnPayIpnResult>.Success(new VnPayIpnResult(order.Id, order.Status.ToString()));
    }
}

