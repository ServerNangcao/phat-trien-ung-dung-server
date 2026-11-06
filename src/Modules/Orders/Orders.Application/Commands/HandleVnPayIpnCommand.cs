using MediatR;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Commands;

/// <summary>
/// Commits a VNPay IPN payment confirmation (server-to-server). The callback payload is
/// parsed and signature-verified by the API layer; this command carries only the validated
/// data. The state machine itself is shared with the MoMo gateway via
/// CompleteOrderPaymentCommand.
/// </summary>
public sealed record HandleVnPayIpnCommand(Guid TransactionId, decimal Amount) : IRequest<Result<VnPayIpnResult>>;

public sealed record VnPayIpnResult(Guid OrderId, string Status);

public sealed class HandleVnPayIpnCommandHandler : IRequestHandler<HandleVnPayIpnCommand, Result<VnPayIpnResult>>
{
    private readonly ISender _sender;

    public HandleVnPayIpnCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<VnPayIpnResult>> Handle(HandleVnPayIpnCommand request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CompleteOrderPaymentCommand(request.TransactionId, request.Amount), cancellationToken);
        return result.Match(
            success => Result<VnPayIpnResult>.Success(new VnPayIpnResult(success.OrderId, success.Status)),
            Result<VnPayIpnResult>.Failure);
    }
}
