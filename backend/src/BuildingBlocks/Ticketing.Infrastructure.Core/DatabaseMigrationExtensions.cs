using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketing.Infrastructure.Core.Outbox;

namespace Ticketing.Infrastructure.Core;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Tự động áp dụng EF Core migrations cho tất cả các DbContext được đăng ký trong hệ thống.
    /// Quét các BaseDbContextMarker đã đăng ký qua DI.
    /// </summary>
    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?
            .CreateLogger("DatabaseMigration");

        var markers = scope.ServiceProvider.GetServices<BaseDbContextMarker>().ToList();
        logger?.LogInformation("Bắt đầu kiểm tra và thực hiện migration cho {Count} DbContext...", markers.Count);

        foreach (var marker in markers)
        {
            try
            {
                if (scope.ServiceProvider.GetService(marker.DbContextType) is DbContext dbContext)
                {
                    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                    if (pendingMigrations.Count > 0)
                    {
                        logger?.LogInformation("Đang áp dụng {Count} migrations cho {ContextName}: {Migrations}",
                            pendingMigrations.Count, marker.DbContextType.Name, string.Join(", ", pendingMigrations));

                        await dbContext.Database.MigrateAsync(cancellationToken);

                        logger?.LogInformation("Hoàn tất migration cho {ContextName}.", marker.DbContextType.Name);
                    }
                    else
                    {
                        logger?.LogDebug("Không có pending migration nào cho {ContextName}.", marker.DbContextType.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Lỗi khi chạy migration cho {ContextName}", marker.DbContextType.Name);
                throw;
            }
        }
    }
}
