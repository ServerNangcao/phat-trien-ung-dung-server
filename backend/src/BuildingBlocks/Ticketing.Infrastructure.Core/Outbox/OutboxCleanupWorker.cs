using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.SharedKernel;

namespace Ticketing.Infrastructure.Core.Outbox;

/// <summary>
/// Background job dọn dữ liệu Outbox và Inbox đã xử lý sau N ngày.
/// Chạy mỗi giờ, xóa batch nhỏ để không block.
/// </summary>
public sealed class OutboxCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxCleanupWorker> _logger;
    private readonly IClock _clock;

    public OutboxCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxCleanupWorker> logger,
        IClock? clock = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _clock = clock ?? new SystemClock();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxCleanupWorker started. Cleanup after {Days} days",
            _options.CleanupAfterDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxCleanupWorker encountered an error");
            }

            // Chạy mỗi giờ
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var markers = scope.ServiceProvider.GetServices<BaseDbContextMarker>();
        var cutoff = _clock.UtcNow.AddDays(-_options.CleanupAfterDays);
        const int batchSize = 1000;

        foreach (var marker in markers)
        {
            if (scope.ServiceProvider.GetService(marker.DbContextType) is not BaseDbContext dbContext)
            {
                continue;
            }

            var totalOutboxDeleted = 0;
            while (true)
            {
                var outboxIds = await dbContext.OutboxMessages
                    .Where(x => x.ProcessedAt != null && x.ProcessedAt < cutoff)
                    .Select(x => x.Id)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (outboxIds.Count == 0) break;

                var deleted = await dbContext.OutboxMessages
                    .Where(x => outboxIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);

                totalOutboxDeleted += deleted;
                if (outboxIds.Count < batchSize) break;
            }

            var totalInboxDeleted = 0;
            while (true)
            {
                var inboxIds = await dbContext.InboxMessages
                    .Where(x => x.ProcessedAt < cutoff)
                    .Select(x => x.Id)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (inboxIds.Count == 0) break;

                var deleted = await dbContext.InboxMessages
                    .Where(x => inboxIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);

                totalInboxDeleted += deleted;
                if (inboxIds.Count < batchSize) break;
            }

            if (totalOutboxDeleted > 0 || totalInboxDeleted > 0)
            {
                _logger.LogInformation(
                    "Cleanup schema '{Schema}': deleted {OutboxCount} outbox, {InboxCount} inbox messages",
                    dbContext.Schema, totalOutboxDeleted, totalInboxDeleted);
            }
        }
    }
}
