using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Application.Services;

/// <summary>
/// The send lifecycle every email goes through: attempt the send, record Sent or Failed
/// (+ linear backoff), commit. First-attempt deliveries and the retry worker both go
/// through this seam so the two paths can never drift apart.
/// </summary>
public interface IEmailDispatcher
{
    Task DeliverAsync(EmailHistory email, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class EmailDispatcher : IEmailDispatcher
{
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(IEmailRepository emailRepository, IEmailService emailService,
        IUnitOfWork unitOfWork, IClock clock, ILogger<EmailDispatcher> logger)
    {
        _emailRepository = emailRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task DeliverAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailService.SendAsync(email.To, email.Subject, email.Body, true, cancellationToken);
            email.MarkSent(_clock.UtcNow);
            _logger.LogInformation("Email {EmailId} sent to {Email} for order {OrderId}", email.Id, email.To, email.OrderId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            email.MarkFailed(ex.Message);
            // Linear backoff (10s, 20s, ...) keeps early retries inside the 30s window.
            email.ScheduleRetry(_clock.UtcNow.AddSeconds(Math.Min(10 * email.RetryCount, 60)));
            _logger.LogWarning(ex, "Email {EmailId} to {Email} failed (attempt {RetryCount}); retry scheduled",
                email.Id, email.To, email.RetryCount);
        }

        await _emailRepository.UpdateAsync(email, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
