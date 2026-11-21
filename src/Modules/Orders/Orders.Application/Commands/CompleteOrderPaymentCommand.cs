using MediatR;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Commits a verified payment confirmation from any gateway (VNPay IPN, MoMo IPN). The
/// transport layer verifies the gateway signature; this command carries only the validated
/// data and owns the full state machine: idempotent replay, amount cross-check, ADR-0003
/// late-payment handling with the automatic refund request.
/// </summary>
public sealed record CompleteOrderPaymentCommand(Guid OrderId, decimal Amount) : IRequest<Result<PaymentCompletionResult>>;

public sealed record PaymentCompletionResult(Guid OrderId, string Status);

public sealed class CompleteOrderPaymentCommandHandler : IRequestHandler<CompleteOrderPaymentCommand, Result<PaymentCompletionResult>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketingModuleApi _ticketingModuleApi;
    private readonly IClock _clock;

    public CompleteOrderPaymentCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork,
        ITicketingModuleApi ticketingModuleApi, IClock clock)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _ticketingModuleApi = ticketingModuleApi;
        _clock = clock;
    }

    public async Task<Result<PaymentCompletionResult>> Handle(CompleteOrderPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<PaymentCompletionResult>.Failure(Error.NotFound($"Order {request.OrderId} not found."));

        // Idempotency: gateways retry IPN until acknowledged; replays must never re-process.
        if (order.Status is OrderStatus.Paid or OrderStatus.PaidAfterExpiry
            or OrderStatus.Refunded or OrderStatus.RefundRequested or OrderStatus.Cancelled)
        {
            return Result<PaymentCompletionResult>.Success(new PaymentCompletionResult(order.Id, order.Status.ToString()));
        }

        if (order.TotalAmount != request.Amount)
            return Result<PaymentCompletionResult>.Failure(Error.Validation(
                $"Amount mismatch: order {order.TotalAmount} vs paid {request.Amount}."));

        // ADR-0003: ask the Ticketing module whether the reservation is still alive. If the
        // hold expired (and stock was released), the money arrived but tickets can no longer
        // be issued: mark PaidAfterExpiry and auto-create the refund request.
        var reservationResult = await _ticketingModuleApi.GetReservationAsync(order.ReservationId, cancellationToken);
        if (reservationResult.IsFailure)
        {
            // Transient failure: do not guess. Return failure so the gateway retries the IPN later.
            return Result<PaymentCompletionResult>.Failure(reservationResult.Error);
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

        return Result<PaymentCompletionResult>.Success(new PaymentCompletionResult(order.Id, order.Status.ToString()));
    }
}
