using CheckIn.Internal.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core;

namespace CheckIn.Internal.Api;

/// <summary>
/// Điểm vào duy nhất (public) của module CheckIn.
/// Đăng ký DI và map endpoints.
/// </summary>
public static class CheckInModule
{
    public static IServiceCollection AddCheckInModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModuleDbContext<CheckInDbContext>(
            connectionStringName: "DefaultConnection",
            schema: CheckInDbContext.SchemaName,
            configuration: configuration);

        services.AddModuleValidators(typeof(CheckInModule).Assembly);

        return services;
    }

    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO: Map các API endpoints
        return app;
    }
}
