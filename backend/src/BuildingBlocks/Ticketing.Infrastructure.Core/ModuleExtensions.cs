using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.SharedKernel;

namespace Ticketing.Infrastructure.Core;

/// <summary>
/// Extension methods để mỗi module đăng ký DbContext, validators, event handlers
/// theo khuôn mẫu thống nhất. Đảm bảo:
/// - Schema riêng + migration history table riêng
/// - FluentValidation quét cả class internal
/// - Outbox worker biết được DbContext của module
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Đăng ký DbContext cho module với schema riêng và migration history table riêng.
    /// Cũng đăng ký BaseDbContextMarker để OutboxWorker tìm được.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionStringName,
        string schema,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
        where TContext : BaseDbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(connectionStringName),
                npgsqlOptions =>
                {
                    // Bảng migration history riêng trong schema của module (điểm 8)
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                });

            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Đăng ký marker để OutboxWorker tìm được tất cả DbContext
        services.AddSingleton(new BaseDbContextMarker(typeof(TContext)));

        return services;
    }

    /// <summary>
    /// Đăng ký FluentValidation validators từ assembly của module.
    /// includeInternalTypes: true để quét được class internal (rủi ro #1 trong kế hoạch).
    /// </summary>
    public static IServiceCollection AddModuleValidators<TModuleMarker>(this IServiceCollection services)
    {
        return services.AddModuleValidators(typeof(TModuleMarker).Assembly);
    }

    /// <summary>
    /// Đăng ký FluentValidation validators từ assembly được chỉ định.
    /// includeInternalTypes: true để quét được class internal.
    /// </summary>
    public static IServiceCollection AddModuleValidators(
        this IServiceCollection services,
        System.Reflection.Assembly assembly)
    {
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }

    /// <summary>
    /// Đăng ký một IntegrationEvent handler và mapping cho OutboxWorker.
    /// Handler phải là internal class trong module nhận event.
    /// </summary>
    public static IServiceCollection RegisterIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        services.AddScoped<THandler>();

        // Đăng ký vào registry tĩnh — OutboxWorker dùng để dispatch
        OutboxWorker.RegisterHandler(
            typeof(TEvent).Name,
            typeof(TEvent),
            typeof(IIntegrationEventHandler<TEvent>));

        return services;
    }
}
