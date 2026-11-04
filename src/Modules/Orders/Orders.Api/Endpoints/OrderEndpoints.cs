using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapPost("/", async (
            [FromBody] CreateOrderRequest request,
            [FromServices] ISender sender) =>
        {
            var result = await sender.Send(new CreateOrderCommand(
                request.CustomerId,
                request.Items.Select(i => new OrderItemDto(i.TicketTypeId, i.Quantity, i.Price)).ToList()));

            return result.Match<IResult>(
                id => Results.Ok(new { OrderId = id }),
                error => Results.BadRequest(new { error.Code, error.Message }));
        })
        .WithName("CreateOrder")
        .WithOpenApi();
    }
}

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<OrderItemRequest> Items);
public sealed record OrderItemRequest(Guid TicketTypeId, int Quantity, decimal Price);