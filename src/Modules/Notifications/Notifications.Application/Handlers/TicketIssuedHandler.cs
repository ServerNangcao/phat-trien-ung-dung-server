using MediatR;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Domain.ValueObjects;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Application.Handlers;

/// <summary>
/// Sends the ticket email (with embedded QR code) when a TicketIssued event arrives.
/// Inbox dedupe happens in the delivery endpoint before this handler runs, so each
/// event sends at most one email.
/// </summary>
public sealed class TicketIssuedHandler : INotificationHandler<TicketIssuedIntegrationEvent>
{
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailService _emailService;
    private readonly IClock _clock;
    private readonly ILogger<TicketIssuedHandler> _logger;

    public TicketIssuedHandler(IEmailRepository emailRepository, IEmailService emailService,
        IClock clock, ILogger<TicketIssuedHandler> logger)
    {
        _emailRepository = emailRepository;
        _emailService = emailService;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(TicketIssuedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // Fall back to a locally generated QR when Ticketing did not supply one, so the
        // email always carries a scannable code.
        var qrImage = string.IsNullOrWhiteSpace(notification.QrCodeUrl)
            ? QrCodeImageGenerator.GenerateDataUri($"TICKET:{notification.TicketId}|ORDER:{notification.OrderId}")
            : notification.QrCodeUrl;

        var template = BuildTicketEmailTemplate(notification);
        var body = RenderBody(template.BodyTemplate, notification, qrImage);

        var record = EmailHistory.Create(
            notification.OrderId,
            notification.CustomerEmail,
            $"Your ticket for {notification.EventName}",
            body,
            _clock.UtcNow);

        if (record.IsFailure)
        {
            _logger.LogError("Failed to create email history record: {Error}", record.Error.Message);
            return;
        }

        var email = record.Value;
        await _emailRepository.AddAsync(email, cancellationToken);

        try
        {
            await _emailService.SendAsync(email.To, template.SubjectTemplate, email.Body, true, cancellationToken);
            email.MarkSent(_clock.UtcNow);
            await _emailRepository.UpdateAsync(email, cancellationToken);
            _logger.LogInformation("Ticket email sent to {Email} for order {OrderId}", email.To, notification.OrderId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            email.MarkFailed(ex.Message);
            email.ScheduleRetry(_clock.UtcNow.AddSeconds(10));
            await _emailRepository.UpdateAsync(email, cancellationToken);
            _logger.LogError(ex, "Failed to send ticket email to {Email} for order {OrderId}; scheduled for retry",
                email.To, notification.OrderId);
        }
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
