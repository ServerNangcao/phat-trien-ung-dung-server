using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Repositories;

namespace Ticketing.Modules.Notifications.Application.Services;

/// <summary>
/// One retry pass over emails that are due. Invoked by the background worker every 10
/// seconds, so a failed first attempt is retried within the 30s delivery budget.
/// </summary>
public sealed class EmailRetryProcessor
{
    public const int MaxRetries = 5;

    private readonly IEmailRepository _emailRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailRetryProcessor> _logger;

    public EmailRetryProcessor(IEmailRepository emailRepository, IEmailService emailService,
        ILogger<EmailRetryProcessor> logger)
    {
        _emailRepository = emailRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ProcessDueAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var dueEmails = await _emailRepository.GetRetryableAsync(now, 20, cancellationToken);

        foreach (var email in dueEmails)
        {
            try
            {
                await _emailService.SendAsync(email.To, email.Subject, email.Body, true, cancellationToken);
                email.MarkSent(now);
                _logger.LogInformation("Retry delivered email {EmailId} to {To}", email.Id, email.To);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                email.MarkFailed(ex.Message);
                // Linear backoff (10s, 20s, ...) keeps early retries inside the 30s window.
                email.ScheduleRetry(now.AddSeconds(Math.Min(10 * email.RetryCount, 60)));
                _logger.LogWarning(ex, "Retry failed for email {EmailId} (attempt {RetryCount})", email.Id, email.RetryCount);
            }

            await _emailRepository.UpdateAsync(email, cancellationToken);
        }
    }
}
