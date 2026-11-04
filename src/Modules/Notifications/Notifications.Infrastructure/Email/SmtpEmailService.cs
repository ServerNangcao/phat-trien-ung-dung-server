using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SMTP configuration for email sending.
/// </summary>
public sealed class SmtpOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? User { get; init; }
    public string? Password { get; init; }
    public string? From { get; init; }
    public bool UseSsl { get; init; } = true;
}

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From ?? "noreply@example.com"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        await SendAsyncInternal(message, cancellationToken);
    }

    public async Task SendAsync(IReadOnlyList<string> to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From ?? "noreply@example.com"));

        foreach (var recipient in to)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        await SendAsyncInternal(message, cancellationToken);
    }

    private async Task SendAsyncInternal(MimeMessage message, CancellationToken cancellationToken)
    {
        // Transient retry keeps a flaky connection inside the 30s email delivery budget:
        // worst case 3 attempts × 8s bound + 2s + 4s backoff ≈ 30s. Authentication
        // failures are permanent and not retried. A final-attempt timeout surfaces as
        // TimeoutException so the caller records the failure (the Pending sweep is only
        // the crash-recovery net).
        const int maxAttempts = 3;

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                // Hard 8s bound per attempt so a hung SMTP endpoint cannot stall the pipeline.
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(TimeSpan.FromSeconds(8));

                try
                {
                    using var client = new SmtpClient();
                    await client.ConnectAsync(
                        _options.Host ?? "smtp.example.com",
                        _options.Port,
                        _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                        attemptCts.Token);
                    await client.AuthenticateAsync(_options.User ?? "", _options.Password ?? "", attemptCts.Token);
                    await client.SendAsync(message, attemptCts.Token);
                    await client.DisconnectAsync(true, attemptCts.Token);
                    return;
                }
                catch (Exception ex) when (
                    attempt < maxAttempts
                    && !cancellationToken.IsCancellationRequested
                    && ex is not MailKit.Security.AuthenticationException)
                {
                    // A per-attempt timeout surfaces as OperationCanceledException; the filter
                    // above lets it retry as long as the caller itself has not cancelled.
                    _logger.LogWarning(ex, "SMTP send attempt {Attempt}/{MaxAttempts} failed; retrying", attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"SMTP send timed out after {maxAttempts} attempts.");
        }
    }
}