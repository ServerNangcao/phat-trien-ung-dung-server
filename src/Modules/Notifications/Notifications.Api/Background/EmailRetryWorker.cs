using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Services;

namespace Ticketing.Modules.Notifications.Api.Background;

/// <summary>
/// Background retry loop: every 10 seconds it hands due emails to the
/// EmailRetryProcessor, so a failed first send is retried inside the 30s budget.
/// </summary>
public sealed class EmailRetryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailRetryWorker> _logger;

    public EmailRetryWorker(IServiceScopeFactory scopeFactory, ILogger<EmailRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EmailRetryProcessor>();
                await processor.ProcessDueAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email retry pass failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
