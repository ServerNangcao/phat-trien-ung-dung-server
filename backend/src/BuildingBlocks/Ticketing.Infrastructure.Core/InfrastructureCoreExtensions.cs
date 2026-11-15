using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.SharedKernel;

namespace Ticketing.Infrastructure.Core;

/// <summary>
/// Extension method để Host đăng ký hạ tầng dùng chung:
/// IClock, OutboxWorker, OutboxCleanupWorker, OutboxOptions.
/// Gọi một lần trong Program.cs.
/// </summary>
public static class InfrastructureCoreExtensions
{
    public static IServiceCollection AddInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // System clock
        services.AddSingleton<IClock, SystemClock>();

        // Outbox options
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        // Background workers
        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<OutboxCleanupWorker>();

        return services;
    }
}
