using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Internal.Infrastructure;
using Ticketing.Infrastructure.Core;

namespace Orders.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module Orders.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersDbContext>(
            connectionStringName: "DefaultConnection",
            schema: OrdersDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(OrdersModule).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO: Map các API endpoints
        return app;
    }
}
