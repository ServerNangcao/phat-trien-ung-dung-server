using MediatR;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Admin approves or rejects a refund request.
/// </summary>
public sealed record ProcessRefundCommand(Guid RefundRequestId, bool Approve, string? AdminNote = null) : IRequest<Result<Guid>>;

public sealed class ProcessRefundCommandHandler : IRequestHandler<ProcessRefundCommand, Result<Guid>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ProcessRefundCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        var refund = await _orderRepository.GetRefundRequestByIdAsync(request.RefundRequestId, cancellationToken);
        if (refund is null)
            return Result<Guid>.Failure(Error.NotFound($"Refund request {request.RefundRequestId} not found."));

        if (refund.Status != RefundStatus.Pending)
            return Result<Guid>.Failure(Error.Validation($"Refund request {refund.Id} is not pending."));

        var order = await _orderRepository.GetByIdAsync(refund.OrderId, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure(Error.NotFound($"Order {refund.OrderId} not found."));

        var now = _clock.UtcNow;

        if (request.Approve)
        {
            // Approval completes a customer-initiated refund (RefundRequested) or the
            // mandatory ADR-0003 late-payment refund (PaidAfterExpiry).
            if (order.Status is not (OrderStatus.RefundRequested or OrderStatus.PaidAfterExpiry))
                return Result<Guid>.Failure(Error.Validation("Cannot approve refund: the order is not awaiting a refund."));

            order.MarkAsRefunded(now);
            refund.MarkAsProcessed(now, request.AdminNote);
        }
        else
        {
            if (order.Status != OrderStatus.RefundRequested)
                return Result<Guid>.Failure(Error.Validation("Cannot reject refund: the order is not in RefundRequested state."));

            order.MarkRefundRejected(now);
            refund.MarkAsRejected(now, request.AdminNote);
        }

        await _unitOfWork.CommitAsync(cancellationToken);
        return Result<Guid>.Success(refund.Id);
    }
}