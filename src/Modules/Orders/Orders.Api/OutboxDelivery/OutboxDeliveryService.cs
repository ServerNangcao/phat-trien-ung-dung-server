using System.Text;
using Ticketing.Infrastructure.Core.Outbox;

namespace Ticketing.Modules.Orders.Api.OutboxDelivery;

/// <summary>
/// Module-local delivery worker: polls orders.outbox_messages and POSTs each integration
/// event to its configured subscriber endpoints. In production, TV1's central worker
/// scans the same table with FOR UPDATE SKIP LOCKED; this service keeps the payment flow
/// end-to-end observable in local development without the central host.
/// </summary>
public sealed class OutboxDeliveryService : BackgroundService
{
    private const int MaxRetries = 5;

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxDeliveryService> _logger;

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
        using var scope = _scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var messages = await outbox.GetUnprocessedAsync(100, cancellationToken);

        foreach (var message in messages)
        {
            var subscribers = _configuration
                .GetSection($"IntegrationDelivery:Subscribers:{ShortName(message.Type)}")
                .Get<string[]>() ?? Array.Empty<string>();

            if (subscribers.Length == 0)
            {
                // No subscriber configured in this deployment; drain the row so the table does not grow.
                await outbox.MarkProcessedAsync(message.Id, cancellationToken);
                continue;
            }

            string? lastError = null;
            var delivered = false;

            foreach (var url in subscribers)
            {
                try
                {
                    using var content = new StringContent(message.Content, Encoding.UTF8, "application/json");
                    using var response = await HttpClient.PostAsync(url, content, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        delivered = true;
                        break;
                    }

                    lastError = $"{url} returned {(int)response.StatusCode}";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = $"{url}: {ex.Message}";
                }
            }

            if (delivered)
            {
                await outbox.MarkProcessedAsync(message.Id, cancellationToken);
                _logger.LogInformation("Delivered outbox message {MessageId} ({EventType})", message.Id, message.Type);
            }
            else
            {
                await outbox.MarkFailedAsync(message.Id, lastError ?? "Delivery failed", cancellationToken);
            }
        }
    }

    private static string ShortName(string fullTypeName) => fullTypeName[(fullTypeName.LastIndexOf('.') + 1)..];
}
