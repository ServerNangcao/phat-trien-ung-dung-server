using Events.Internal.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core;

namespace Events.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module Events.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class EventsModule
{
    public static IServiceCollection AddEventsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<EventsDbContext>(
            connectionStringName: "DefaultConnection",
            schema: EventsDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(EventsModule).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO: Map các API endpoints
        return app;
    }
}
