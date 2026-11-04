using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Domain.ValueObjects;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Application.Services;

public sealed record PreparedTicketEmail(bool IsNew, EmailHistory? Email);

/// <summary>
/// Ticket-email lifecycle. PrepareIncomingAsync records the inbox row and the Pending
/// email history in one commit, so a crash after the 202 ack is recovered by the retry
/// worker's Pending sweep and no accepted event can lose its email. DeliverAsync is the
/// single send lifecycle (send → Sent, or Failed + backoff) reused by first delivery
/// and by the retry worker.
/// </summary>
public sealed class TicketEmailProcessor
{
    private readonly IInbox _inbox;
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<TicketEmailProcessor> _logger;

    public TicketEmailProcessor(IInbox inbox, IEmailRepository emailRepository, IEmailService emailService,
        IUnitOfWork unitOfWork, IClock clock, ILogger<TicketEmailProcessor> logger)
    {
        _inbox = inbox;
        _emailRepository = emailRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Atomically records inbox + Pending email history. Returns IsNew=false for a
    /// redelivered event. Email is null when the event was consumed but carries no
    /// sendable payload (invalid recipient data).
    /// </summary>
    public async Task<PreparedTicketEmail> PrepareIncomingAsync(TicketIssuedIntegrationEvent notification,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(notification);
        var isNew = await _inbox.TryStageAsync(notification.EventId, "TicketIssued", payload, cancellationToken);
        if (!isNew)
            return new PreparedTicketEmail(false, null);

        var qrImage = string.IsNullOrWhiteSpace(notification.QrCodeUrl)
            ? QrCodeImageGenerator.GenerateDataUri($"TICKET:{notification.TicketId}|ORDER:{notification.OrderId}")
            : notification.QrCodeUrl;

        var template = BuildTicketEmailTemplate(notification);
        var record = EmailHistory.Create(
            notification.OrderId,
            notification.CustomerEmail,
            $"Your ticket for {notification.EventName}",
            RenderBody(template.BodyTemplate, notification, qrImage),
            _clock.UtcNow);

        if (record.IsFailure)
        {
            // Invalid payload: the event is consumed (inbox row commits) but nothing is sendable.
            _logger.LogError("Rejected TicketIssued event {EventId}: {Error}", notification.EventId, record.Error.Message);
            await _unitOfWork.CommitAsync(cancellationToken);
            return new PreparedTicketEmail(true, null);
        }

        await _emailRepository.AddAsync(record.Value, cancellationToken);

        var commit = await _unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
            return new PreparedTicketEmail(false, null); // concurrent duplicate lost the unique-index race

        return new PreparedTicketEmail(true, record.Value);
    }

    /// <summary>
    /// The single send lifecycle: attempt the send, record Sent or Failed (+backoff), commit.
    /// </summary>
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

    private static EmailTemplate BuildTicketEmailTemplate(TicketIssuedIntegrationEvent notification)
    {
        var subject = $"[{notification.EventName}] Your ticket confirmation";
        var body = $@"
            <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2>Your Ticket for {{{{EventName}}}}</h2>
                    <p>Dear customer,</p>
                    <p>Your ticket has been issued successfully.</p>
                    <table style='border-collapse: collapse; width: 100%;'>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Event</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{{{{EventName}}}}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Date</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{{{{EventDate}}}}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Ticket Type</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{{{{TicketType}}}}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Order ID</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{{{{OrderId}}}}</td>
                        </tr>
                    </table>
                    <p>Please present the QR code below at the entrance:</p>
                    <img src='{{{{QrCodeUrl}}}}' alt='Ticket QR Code' style='max-width: 200px;' />
                    <p>Thank you for your purchase!</p>
                </body>
            </html>";

        var template = EmailTemplate.Create("ticket-confirmation", subject, body);
        return template.IsSuccess
            ? template.Value
            : throw new InvalidOperationException($"Failed to build email template: {template.Error.Message}");
    }

    private static string RenderBody(string template, TicketIssuedIntegrationEvent notification, string qrImage)
    {
        return template
            .Replace("{{EventName}}", HtmlEncode(notification.EventName))
            .Replace("{{EventDate}}", HtmlEncode(notification.EventDate))
            .Replace("{{TicketType}}", HtmlEncode(notification.TicketType))
            .Replace("{{OrderId}}", notification.OrderId.ToString())
            .Replace("{{QrCodeUrl}}", qrImage);
    }

    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
