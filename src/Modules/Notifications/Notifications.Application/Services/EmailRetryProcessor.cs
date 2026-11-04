using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Repositories;

namespace Ticketing.Modules.Notifications.Application.Services;

/// <summary>
/// One retry pass over emails that are due. Invoked by the background worker every 10
/// seconds, so a failed first attempt is retried within the 30s delivery budget.
/// Delivery itself is delegated to TicketEmailProcessor.DeliverAsync — the same lifecycle
/// as first-attempt sends.
/// </summary>
public sealed class EmailRetryProcessor
{
    public const int MaxRetries = 5;

    private readonly IEmailRepository _emailRepository;
    private readonly TicketEmailProcessor _ticketEmailProcessor;
    private readonly ILogger<EmailRetryProcessor> _logger;

    public EmailRetryProcessor(IEmailRepository emailRepository, TicketEmailProcessor ticketEmailProcessor,
        ILogger<EmailRetryProcessor> logger)
    {
        _emailRepository = emailRepository;
        _ticketEmailProcessor = ticketEmailProcessor;
        _logger = logger;
    }

    public async Task ProcessDueAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var dueEmails = await _emailRepository.GetRetryableAsync(now, 20, cancellationToken);

        foreach (var email in dueEmails)
        {
            _logger.LogInformation("Retrying email {EmailId} to {To} (attempt {RetryCount})",
                email.Id, email.To, email.RetryCount);
            await _ticketEmailProcessor.DeliverAsync(email, cancellationToken);
        }
    }
}
