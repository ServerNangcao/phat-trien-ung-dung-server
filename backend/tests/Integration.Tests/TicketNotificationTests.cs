using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Internal.Application;
using Notifications.Internal.Domain;
using Notifications.Internal.Infrastructure;
using Ticketing.Contracts;
using Xunit;

namespace Integration.Tests;

public class TicketNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NotificationsDbContext> _options;

    public TicketNotificationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new NotificationsDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Điểm 2 & Điểm 4: Notifications nhận TicketIssuedIntegrationEvent, ghi Inbox, gửi email vé.
    /// Nếu event được gửi lần 2 (duplicate), Inbox chặn lại và không gửi email trùng.
    /// </summary>
    [Fact]
    public async Task TicketIssued_Event_Processed_Once_And_Duplicate_Skipped_By_Inbox()
    {
        var emailService = new EmailService(NullLogger<EmailService>.Instance);
        var logger = NullLogger<TicketIssuedEventHandler>.Instance;

        var integrationEvent = new TicketIssuedIntegrationEvent
        {
            OrderId = Guid.NewGuid(),
            TargetEventId = Guid.NewGuid(),
            EventTitle = "Đêm Nhạc Rock Mùa Thu 2026",
            CustomerEmail = "khachhang@example.com",
            CustomerName = "Nguyễn Văn A",
            Tickets =
            [
                new IssuedTicketDto
                {
                    TicketId = Guid.NewGuid(),
                    TicketCode = "TCK-ROCK-001",
                    TicketTypeName = "VIP Khán Đài A",
                    Price = 1500000m,
                    QrPayload = "HMAC_SIGNED_PAYLOAD_001"
                }
            ]
        };

        // Lần 1: Xử lý event
        using (var context = new NotificationsDbContext(_options))
        {
            var handler = new TicketIssuedEventHandler(context, emailService, logger);
            await handler.HandleAsync(integrationEvent, CancellationToken.None);
        }

        // Kiểm tra sau lần 1: Có 1 InboxMessage và 1 EmailNotification
        using (var context = new NotificationsDbContext(_options))
        {
            var inboxCount = await context.InboxMessages.CountAsync(x => x.EventId == integrationEvent.EventId);
            Assert.Equal(1, inboxCount);

            var emails = await context.EmailNotifications.ToListAsync();
            Assert.Single(emails);
            Assert.Equal("khachhang@example.com", emails[0].Recipient);
            Assert.Equal(EmailNotificationStatus.Sent, emails[0].Status);
            Assert.Contains("TCK-ROCK-001", emails[0].Body);
        }

        // Lần 2: Giao lại chính event đó (retry / duplicate)
        using (var context = new NotificationsDbContext(_options))
        {
            var handler = new TicketIssuedEventHandler(context, emailService, logger);
            await handler.HandleAsync(integrationEvent, CancellationToken.None);
        }

        // Kiểm tra sau lần 2: Vẫn chỉ có 1 EmailNotification (không bị gửi lặp)
        using (var context = new NotificationsDbContext(_options))
        {
            var emailCount = await context.EmailNotifications.CountAsync();
            Assert.Equal(1, emailCount);
        }
    }
}
