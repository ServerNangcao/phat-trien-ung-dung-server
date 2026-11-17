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
    private readonly IEmailDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<TicketEmailProcessor> _logger;

    public TicketEmailProcessor(IInbox inbox, IEmailRepository emailRepository, IEmailDispatcher dispatcher,
        IUnitOfWork unitOfWork, IClock clock, ILogger<TicketEmailProcessor> logger)
    {
        _inbox = inbox;
        _emailRepository = emailRepository;
        _dispatcher = dispatcher;
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

        // The inbound event is unauthenticated at the module boundary: only accept QR
        // references that are plain http(s) URLs; anything else falls back to a locally
        // generated QR, so a crafted event can never inject markup into the email HTML.
        var qrImage = IsSafeQrReference(notification.QrCodeUrl)
            ? notification.QrCodeUrl
            : QrCodeImageGenerator.GenerateDataUri($"TICKET:{notification.TicketId}|ORDER:{notification.OrderId}");

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
    /// Delegates the send lifecycle to the shared EmailDispatcher (same path as first
    /// delivery and the retry worker).
    /// </summary>
    public Task DeliverAsync(EmailHistory email, CancellationToken cancellationToken = default)
        => _dispatcher.DeliverAsync(email, cancellationToken);

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
                    {{{{InvoiceSection}}}}
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

    /// <summary>
    /// Invoice table with per-ticket-type pricing. Empty when the publisher did not
    /// supply pricing, so older event payloads keep rendering without the section.
    /// </summary>
    private static string BuildInvoiceSection(TicketIssuedIntegrationEvent notification)
    {
        if (notification.TotalAmount <= 0)
            return string.Empty;

        var rows = notification.Items.Count > 0
            ? string.Join("", notification.Items.Select(i => $@"
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{HtmlEncode(i.TicketTypeName)}</td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{i.Quantity}</td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{FormatVnd(i.UnitPrice)}</td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{FormatVnd(i.Quantity * i.UnitPrice)}</td>
                        </tr>"))
            : $@"
                        <tr>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{HtmlEncode(notification.TicketType)}</td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>1</td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{FormatVnd(notification.TotalAmount)}</td>
                            <td style='border: 1px solid #ddd; padding: 8px;'>{FormatVnd(notification.TotalAmount)}</td>
                        </tr>";

        return $@"
                    <h3>Invoice</h3>
                    <table style='border-collapse: collapse; width: 100%;'>
                        <tr style='background-color: #f2f2f2;'>
                            <th style='border: 1px solid #ddd; padding: 8px;'>Ticket</th>
                            <th style='border: 1px solid #ddd; padding: 8px;'>Qty</th>
                            <th style='border: 1px solid #ddd; padding: 8px;'>Price</th>
                            <th style='border: 1px solid #ddd; padding: 8px;'>Subtotal</th>
                        </tr>{rows}
                        <tr>
                            <td colspan='3' style='border: 1px solid #ddd; padding: 8px; text-align: right;'><strong>Total</strong></td>
                            <td style='border: 1px solid #ddd; padding: 8px;'><strong>{FormatVnd(notification.TotalAmount)}</strong></td>
                        </tr>
                    </table>";
    }

    /// <summary>Culture-stable VND formatting: email HTML must not vary by server locale.</summary>
    private static string FormatVnd(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " VND";

    private static string RenderBody(string template, TicketIssuedIntegrationEvent notification, string qrImage)
    {
        return template
            .Replace("{{EventName}}", HtmlEncode(notification.EventName))
            .Replace("{{EventDate}}", HtmlEncode(notification.EventDate))
            .Replace("{{TicketType}}", HtmlEncode(notification.TicketType))
            .Replace("{{OrderId}}", notification.OrderId.ToString())
            .Replace("{{InvoiceSection}}", BuildInvoiceSection(notification))
            .Replace("{{QrCodeUrl}}", HtmlAttributeEncode(qrImage));
    }

    private static bool IsSafeQrReference(string url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https";
    }

    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);

    /// <summary>Encodes a value for use inside a single-quoted HTML attribute.</summary>
    private static string HtmlAttributeEncode(string value)
        => HtmlEncode(value).Replace("'", "&#39;");
}
