using System.Text;
using Ticketing.Infrastructure.Core.Outbox;

namespace Ticketing.Modules.Orders.Api.OutboxDelivery;

/// <summary>
/// Module-local delivery worker: polls orders.outbox_messages and POSTs each integration
/// event to its configured subscriber endpoints. In production, TV1's central worker
/// (FOR UPDATE SKIP LOCKED claim) replaces this service — enable only one of them.
/// Failed messages back off (10s per retry count) and dead-letter after 5 attempts;
/// rows are never drained silently, so a missing subscriber configuration is visible.
/// </summary>
public sealed class OutboxDeliveryService : BackgroundService
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(10);

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxDeliveryService> _logger;
    private readonly HashSet<string> _warnedMissingSubscribers = new();

    public OutboxDeliveryService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<OutboxDeliveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_configuration.GetValue("IntegrationDelivery:PollIntervalSeconds", 2));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeliverPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox delivery batch failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task DeliverPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        using var scope = _scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var messages = await outbox.GetUnprocessedAsync(100, cancellationToken);

        foreach (var message in messages)
        {
            // Backoff: skip messages whose retry window has not elapsed.
            if (message.LastAttemptAt is { } lastAttempt && now - lastAttempt < RetryBackoff * message.RetryCount)
                continue;

            var subscribers = _configuration
                .GetSection($"IntegrationDelivery:Subscribers:{ShortName(message.Type)}")
                .Get<string[]>() ?? Array.Empty<string>();

            if (subscribers.Length == 0)
            {
                // Never drain silently: an unconfigured subscriber must stay visible as an
                // undelivered row (and alert via logs) rather than be marked processed.
                if (_warnedMissingSubscribers.Add(message.Type))
                {
                    _logger.LogWarning(
                        "No subscriber configured for {EventType}; outbox message {MessageId} stays undelivered",
                        message.Type, message.Id);
                }

                continue;
            }

            // Fan out to every subscriber; the row is processed only when all of them accept.
            var failures = new List<string>();
            foreach (var url in subscribers)
            {
                try
                {
                    using var content = new StringContent(message.Content, Encoding.UTF8, "application/json");
                    using var response = await HttpClient.PostAsync(url, content, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        failures.Add($"{url} returned {(int)response.StatusCode}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures.Add($"{url}: {ex.Message}");
                }
            }

            if (failures.Count == 0)
            {
                await outbox.MarkProcessedAsync(message.Id, cancellationToken);
                _logger.LogInformation("Delivered outbox message {MessageId} ({EventType}) to {SubscriberCount} subscriber(s)",
                    message.Id, message.Type, subscribers.Length);
            }
            else
            {
                await outbox.MarkFailedAsync(message.Id, string.Join("; ", failures), cancellationToken);
            }
        }
    }

    private static string ShortName(string fullTypeName) => fullTypeName[(fullTypeName.LastIndexOf('.') + 1)..];
}
