using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Domain.ValueObjects;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Application.Services;

/// <summary>Which customer email a refund notice carries.</summary>
public enum RefundEmailKind
{
    /// <summary>ADR-0003: payment arrived after the reservation expired; a refund request was auto-created.</summary>
    LatePaymentAutoRefund,

    /// <summary>A refund request was approved and the money is being returned.</summary>
    RefundProcessed
}

/// <summary>Everything the processor needs to render one refund notice.</summary>
public sealed record RefundNotice(
    RefundEmailKind Kind,
    Guid EventId,
    Guid OrderId,
    string CustomerEmail,
    decimal TotalAmount,
    string Reason,
    DateTime OccurredAtUtc);

public sealed record PreparedRefundEmail(bool IsNew, EmailHistory? Email);

/// <summary>
/// Refund-email lifecycle (UC-ORD-02): PrepareAsync records the Inbox row and the Pending
/// email history in one commit — the same crash-safety contract as the ticket email — and
/// DeliveredAsync delegates the send lifecycle to the shared EmailDispatcher. An event
/// whose payload carries no usable recipient address is consumed (Inbox row commits) but
/// produces no email; it is never silently dropped from the Inbox.
/// </summary>
public sealed class RefundEmailProcessor
{
    private readonly IInbox _inbox;
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<RefundEmailProcessor> _logger;

    public RefundEmailProcessor(IInbox inbox, IEmailRepository emailRepository, IEmailDispatcher dispatcher,
        IUnitOfWork unitOfWork, IClock clock, ILogger<RefundEmailProcessor> logger)
    {
        _inbox = inbox;
        _emailRepository = emailRepository;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Atomically records Inbox + Pending email history. Returns IsNew=false for a
    /// redelivered event. Email is null when the event was consumed but carries no
    /// sendable recipient address.
    /// </summary>
    public async Task<PreparedRefundEmail> PrepareAsync(RefundNotice notice, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(notice);
        var isNew = await _inbox.TryStageAsync(notice.EventId, notice.Kind.ToString(), payload, cancellationToken);
        if (!isNew)
            return new PreparedRefundEmail(false, null);

        var template = BuildTemplate(notice);
        var record = EmailHistory.Create(
            notice.OrderId,
            notice.CustomerEmail,
            template.Subject,
            template.Body,
            _clock.UtcNow);

        if (record.IsFailure)
        {
            // Invalid recipient (e.g. Orders never learned the email): the event is consumed
            // so a redelivery is still deduped, but nothing is sendable.
            _logger.LogError("Rejected {Kind} event {EventId}: {Error}", notice.Kind, notice.EventId, record.Error.Message);
            await _unitOfWork.CommitAsync(cancellationToken);
            return new PreparedRefundEmail(true, null);
        }

        await _emailRepository.AddAsync(record.Value, cancellationToken);

        var commit = await _unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
            return new PreparedRefundEmail(false, null); // concurrent duplicate lost the unique-index race

        return new PreparedRefundEmail(true, record.Value);
    }

    /// <summary>Sends a prepared email through the shared lifecycle.</summary>
    public Task DeliverAsync(EmailHistory email, CancellationToken cancellationToken = default)
        => _dispatcher.DeliverAsync(email, cancellationToken);

    private static (string Subject, string Body) BuildTemplate(RefundNotice notice)
    {
        var orderId = notice.OrderId.ToString();
        var amount = FormatVnd(notice.TotalAmount);

        return notice.Kind switch
        {
            RefundEmailKind.LatePaymentAutoRefund => (
                $"[Ticketing] Payment received after your reservation expired (order {orderId})",
                $@"
                    <html>
                        <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                            <h2>We're sorry — your tickets were no longer held</h2>
                            <p>Dear customer,</p>
                            <p>Your payment of <strong>{amount}</strong> for order <strong>{orderId}</strong> arrived
                            after the 10-minute ticket hold had expired, so the tickets had been released back to
                            stock and we could not issue them.</p>
                            <p>An automatic refund request has been created for this order. Our team will review
                            and the money will be returned through your payment gateway. No action is needed from you.</p>
                            <table style='border-collapse: collapse; width: 100%;'>
                                <tr>
                                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Order</strong></td>
                                    <td style='border: 1px solid #ddd; padding: 8px;'>{orderId}</td>
                                </tr>
                                <tr>
                                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Amount</strong></td>
                                    <td style='border: 1px solid #ddd; padding: 8px;'>{amount}</td>
                                </tr>
                                <tr>
                                    <td style='border: 1px solid #ddd; padding: 8px;'><strong>Status</strong></td>
                                    <td style='border: 1px solid #ddd; padding: 8px;'>Refund request created — awaiting processing</td>
                                </tr>
                            </table>
                            <p>We apologize for the inconvenience.</p>
                        </body>
                    </html>"),

            _ => (
                $"[Ticketing] Your refund for order {orderId} has been processed",
                $@"
                    <html>
                        <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                            <h2>Your refund has been processed</h2>
                            <p>Dear customer,</p>
                            <p>The refund for order <strong>{orderId}</strong> has been approved and the amount of
                            <strong>{amount}</strong> will be returned through your original payment method.</p>
                            <p>Depending on your bank, it may take a few business days to appear on your statement.</p>
                            <p>Thank you for your understanding.</p>
                        </body>
                    </html>")
        };
    }

    /// <summary>Culture-stable VND formatting: email HTML must not vary by server locale.</summary>
    private static string FormatVnd(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " VND";
}
