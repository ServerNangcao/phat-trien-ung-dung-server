using MediatR;
using Ticketing.Modules.Orders.Application.Queries;
using Ticketing.Modules.Orders.Contracts;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Api;

/// <summary>
/// In-process adapter implementing the Orders module's public contract. The host (TV1)
/// gets this via AddOrdersModule, so the Events module's dashboard (FR-23) calls
/// IOrdersModuleApi without knowing the orders schema. Re-homing to an HTTP adapter
/// later touches only this class and its registration.
/// </summary>
public sealed class OrdersModuleApi : IOrdersModuleApi
{
    private readonly ISender _sender;

    public OrdersModuleApi(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _sender.Send(new GetEventSalesSummaryQuery(eventId), cancellationToken);
    }
}
