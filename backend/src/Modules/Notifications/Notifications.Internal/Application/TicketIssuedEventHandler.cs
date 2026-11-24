using Microsoft.Extensions.Logging;
using Notifications.Internal.Domain;
using Notifications.Internal.Infrastructure;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core.Outbox;

namespace Notifications.Internal.Application;

/// <summary>
/// Handler xử lý sự kiện phát hành vé (TicketIssuedIntegrationEvent) từ module Ticketing (Điểm 2).
/// Sử dụng Inbox để đảm bảo chống xử lý lặp (Điểm 4).
/// </summary>
internal sealed class TicketIssuedEventHandler : IIntegrationEventHandler<TicketIssuedIntegrationEvent>
{
    // Chuỗi Handler đặt tay cố định, không dùng Type.FullName per Điểm 4
    public const string HandlerName = "Notifications.TicketIssuedEventHandler";

    private readonly NotificationsDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<TicketIssuedEventHandler> _logger;

    public TicketIssuedEventHandler(
        NotificationsDbContext dbContext,
        IEmailService emailService,
        ILogger<TicketIssuedEventHandler> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(
        TicketIssuedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Inbox chống lặp (Điểm 4)
        var isNewEvent = await _dbContext.TryAddInboxMessageAsync(
            integrationEvent.EventId,
            HandlerName,
            cancellationToken);

        if (!isNewEvent)
        {
            _logger.LogInformation(
                "Event {EventId} has already been processed by {Handler}. Skipping.",
                integrationEvent.EventId,
                HandlerName);
            return;
        }

        if (string.IsNullOrWhiteSpace(integrationEvent.CustomerEmail))
        {
            _logger.LogWarning(
                "TicketIssuedIntegrationEvent {EventId} for order {OrderId} has empty CustomerEmail. Skipping email.",
                integrationEvent.EventId,
                integrationEvent.OrderId);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // 2. Soạn nội dung email vé điện tử HTML kèm ảnh QR
        var subject = $"[Ticketing Platform] Vé điện tử sự kiện: {integrationEvent.EventTitle}";
        var body = TicketEmailRenderer.RenderTicketIssuedHtml(integrationEvent);

        var notification = EmailNotification.Create(
            integrationEvent.OrderId,
            integrationEvent.CustomerEmail.Trim(),
            subject,
            body);

        try
        {
            // 3. Gửi email
            await _emailService.SendEmailAsync(
                notification.Recipient,
                subject,
                body,
                cancellationToken);

            // 4. Đánh dấu thành công
            notification.MarkSent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ticket email to {Email} for order {OrderId}",
                notification.Recipient, integrationEvent.OrderId);
            notification.MarkFailed(
                ex.Message[..Math.Min(ex.Message.Length, 4000)],
                DateTime.UtcNow.AddMinutes(1));
        }

        _dbContext.EmailNotifications.Add(notification);

        // 5. Lưu InboxMessage và EmailNotification cùng transaction
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
