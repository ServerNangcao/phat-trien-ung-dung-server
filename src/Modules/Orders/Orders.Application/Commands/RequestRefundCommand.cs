using MediatR;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Customer requests a refund for a paid order.
/// </summary>
public sealed record RequestRefundCommand(Guid OrderId, string Reason) : IRequest<Result<Guid>>;

public sealed class RequestRefundCommandHandler : IRequestHandler<RequestRefundCommand, Result<Guid>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RequestRefundCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure(Error.NotFound($"Order {request.OrderId} not found."));

        if (order.Status != OrderStatus.Paid)
            return Result<Guid>.Failure(Error.Validation("Only paid orders can request a refund."));

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<Guid>.Failure(Error.Validation("Refund reason is required."));

        order.RequestRefund(request.Reason, _clock.UtcNow);
        var refundRequest = RefundRequest.Create(order.Id, request.Reason, _clock.UtcNow);
        await _orderRepository.AddRefundRequestAsync(refundRequest, cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);
        return Result<Guid>.Success(refundRequest.Id);
    }
}