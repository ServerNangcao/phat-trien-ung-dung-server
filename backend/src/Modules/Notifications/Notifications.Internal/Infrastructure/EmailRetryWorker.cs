using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Internal.Domain;

namespace Notifications.Internal.Infrastructure;

/// <summary>
/// BackgroundService tự động gửi lại các EmailNotification bị lỗi tạm thời (Status = Failed)
/// hoặc bị kẹt ở trạng thái Pending, dùng backoff lùi dần và tôn trọng index ix_email_retry.
/// </summary>
internal sealed class EmailRetryWorker : BackgroundService
{
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailRetryWorker> _logger;

    public EmailRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SmtpOptions> options,
        ILogger<EmailRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableRetryWorker || !_options.Enabled)
        {
            _logger.LogInformation("EmailRetryWorker is disabled (Enabled={Enabled}, EnableRetryWorker={EnableRetryWorker}).",
                _options.Enabled, _options.EnableRetryWorker);
            return;
        }

        var intervalSeconds = Math.Clamp(_options.RetryIntervalSeconds, 5, 3600);
        _logger.LogInformation("EmailRetryWorker started. Interval: {Interval}s, MaxAttempts: {MaxAttempts}",
            intervalSeconds, _options.MaxRetryAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRetriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailRetryWorker encountered an unhandled error.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    internal async Task<int> ProcessPendingRetriesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var nowUtc = DateTime.UtcNow;
        var stuckPendingThreshold = nowUtc.AddMinutes(-2);
        var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);

        if (db.Database.IsNpgsql())
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var sql = $$"""
                SELECT * FROM "{{db.Schema}}"."EmailNotifications"
                WHERE (("Status" = 'Failed') OR ("Status" = 'Pending' AND "CreatedAt" <= {0}))
                  AND "AttemptCount" < {1}
                  AND ("NextRetryAt" IS NULL OR "NextRetryAt" <= {2})
                ORDER BY COALESCE("NextRetryAt", "CreatedAt")
                LIMIT {{BatchSize}}
                FOR UPDATE SKIP LOCKED
                """;

            var dueNotifications = await db.EmailNotifications
                .FromSqlRaw(sql, stuckPendingThreshold, maxAttempts, nowUtc)
                .ToListAsync(cancellationToken);

            var processed = await RetryBatchAsync(dueNotifications, emailService, maxAttempts, nowUtc, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return processed;
        }

        var items = await db.EmailNotifications
            .Where(n =>
                (n.Status == EmailNotificationStatus.Failed ||
                 (n.Status == EmailNotificationStatus.Pending && n.CreatedAt <= stuckPendingThreshold)) &&
                n.AttemptCount < maxAttempts &&
                (n.NextRetryAt == null || n.NextRetryAt <= nowUtc))
            .OrderBy(n => n.NextRetryAt ?? n.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var count = await RetryBatchAsync(items, emailService, maxAttempts, nowUtc, cancellationToken);
        if (count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    private async Task<int> RetryBatchAsync(
        List<EmailNotification> notifications,
        IEmailService emailService,
        int maxAttempts,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (notifications.Count == 0)
        {
            return 0;
        }

        foreach (var notification in notifications)
        {
            try
            {
                await emailService.SendEmailAsync(
                    notification.Recipient,
                    notification.Subject,
                    notification.Body,
                    cancellationToken);

                notification.MarkSent();
            }
            catch (Exception ex)
            {
                var nextAttemptCount = notification.AttemptCount + 1;
                var nextRetryAt = nextAttemptCount >= maxAttempts
                    ? (DateTime?)null
                    : CalculateNextRetryAt(nowUtc, nextAttemptCount);

                var errorMessage = ex.Message[..Math.Min(ex.Message.Length, 4000)];
                notification.MarkFailed(errorMessage, nextRetryAt);

                if (nextRetryAt is null)
                {
                    _logger.LogError(ex,
                        "EmailNotification {NotificationId} to {Recipient} exhausted {MaxAttempts} attempts.",
                        notification.Id, notification.Recipient, maxAttempts);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Retry failed for EmailNotification {NotificationId} (attempt {Attempt}/{MaxAttempts}). Next retry at {NextRetryAt}.",
                        notification.Id, nextAttemptCount, maxAttempts, nextRetryAt);
                }
            }
        }

        return notifications.Count;
    }

    internal static DateTime CalculateNextRetryAt(DateTime nowUtc, int attemptCount)
    {
        var delayMinutes = attemptCount switch
        {
            <= 1 => 1,
            2 => 5,
            3 => 15,
            _ => 30
        };
        return nowUtc.AddMinutes(delayMinutes);
    }
}
