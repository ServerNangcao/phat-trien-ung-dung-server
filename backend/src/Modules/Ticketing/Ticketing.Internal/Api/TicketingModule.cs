using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module Ticketing.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class TicketingModule
{
    public static IServiceCollection AddTicketingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<TicketingDbContext>(
            connectionStringName: "DefaultConnection",
            schema: TicketingDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(TicketingModule).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapTicketingEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO: Map các API endpoints
        return app;
    }
}
