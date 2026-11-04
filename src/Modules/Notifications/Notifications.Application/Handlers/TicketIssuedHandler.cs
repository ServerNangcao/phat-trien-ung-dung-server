using MediatR;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Domain.ValueObjects;
using Ticketing.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Ticketing.Modules.Notifications.Application.Handlers;

/// <summary>
/// Handler that sends the ticket email (with QR code) when a TicketIssued event arrives.
/// Uses the Inbox pattern for idempotency (handled by the infrastructure layer).
/// </summary>
public sealed class TicketIssuedHandler : INotificationHandler<TicketIssuedIntegrationEvent>
{
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailService _emailService;
    private readonly IClock _clock;
    private readonly ILogger<TicketIssuedHandler> _logger;

    public TicketIssuedHandler(
        IEmailRepository emailRepository,
        IEmailService emailService,
        IClock clock,
        ILogger<TicketIssuedHandler> logger)
    {
        _emailRepository = emailRepository;
        _emailService = emailService;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(TicketIssuedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var template = BuildTicketEmailTemplate(notification);
        var body = RenderBody(template.BodyTemplate, notification);

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

        await _emailRepository.AddAsync(MapToDto(record.Value), cancellationToken);

        try
        {
            await _emailService.SendAsync(notification.CustomerEmail, template.SubjectTemplate, body, true, cancellationToken);
            record.Value.MarkSent(_clock.UtcNow);
            await _emailRepository.UpdateAsync(MapToDto(record.Value), cancellationToken);
            _logger.LogInformation("Ticket email sent to {Email} for order {OrderId}", notification.CustomerEmail, notification.OrderId);
        }
        catch (Exception ex)
        {
            record.Value.MarkFailed();
            await _emailRepository.UpdateAsync(MapToDto(record.Value), cancellationToken);
            _logger.LogError(ex, "Failed to send ticket email to {Email} for order {OrderId}", notification.CustomerEmail, notification.OrderId);
        }
    }

    private static EmailTemplate BuildTicketEmailTemplate(TicketIssuedIntegrationEvent notification)
    {
        var subject = $"[{notification.EventName}] Your ticket confirmation";
        var body = $@"
            <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2>Your Ticket for {notification.EventName}</h2>
                    <p>Dear customer,</p>
                    <p>Your ticket has been issued successfully.</p>
                    <table style='border-collapse: collapse; width: 100%;'>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Event</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{notification.EventName}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Date</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{notification.EventDate}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Ticket Type</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{notification.TicketType}</td>
                        </tr>
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>Order ID</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{notification.OrderId}</td>
                        </tr>
                    </table>
                    <p>Please present the QR code below at the entrance:</p>
                    <img src='{notification.QrCodeUrl}' alt='Ticket QR Code' style='max-width: 200px;' />
                    <p>Thank you for your purchase!</p>
                </body>
            </html>";

        var template = EmailTemplate.Create("ticket-confirmation", subject, body);
        return template.IsSuccess
            ? template.Value
            : throw new InvalidOperationException($"Failed to build email template: {template.Error.Message}");
    }

    private static string RenderBody(string template, TicketIssuedIntegrationEvent notification)
    {
        return template
            .Replace("{{EventName}}", notification.EventName)
            .Replace("{{EventDate}}", notification.EventDate)
            .Replace("{{TicketType}}", notification.TicketType)
            .Replace("{{OrderId}}", notification.OrderId.ToString());
    }

    private static EmailHistoryDto MapToDto(EmailHistory entity)
    {
        return new EmailHistoryDto(
            entity.Id,
            entity.OrderId,
            entity.To,
            entity.Subject,
            entity.Body,
            entity.Status.ToString(),
            entity.SentAt,
            entity.CreatedAt,
            entity.RetryCount
        );
    }
}