using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
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

    [Fact]
    public void TicketEmailRenderer_Encodes_Html_And_Embeds_Valid_Qr_Png()
    {
        var integrationEvent = new TicketIssuedIntegrationEvent
        {
            OrderId = Guid.NewGuid(),
            TargetEventId = Guid.NewGuid(),
            EventTitle = "Show <script>alert(1)</script>",
            CustomerEmail = "buyer@example.com",
            CustomerName = "<b>Customer</b>",
            Tickets =
            [
                new IssuedTicketDto
                {
                    TicketId = Guid.NewGuid(),
                    TicketCode = "TCK-QR-101",
                    TicketTypeName = "Standard",
                    Price = 250000m,
                    QrPayload = "PAYLOAD-TEST-101"
                }
            ]
        };

        var html = TicketEmailRenderer.RenderTicketIssuedHtml(integrationEvent);

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>Customer</b>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&lt;b&gt;Customer&lt;/b&gt;", html);
        Assert.Contains("TCK-QR-101", html);

        const string marker = "data:image/png;base64,";
        var startIdx = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(startIdx >= 0);
        startIdx += marker.Length;
        var endIdx = html.IndexOf('"', startIdx);
        var base64 = html[startIdx..endIdx];
        var pngBytes = Convert.FromBase64String(base64);

        // PNG magic bytes: 89 50 4E 47
        Assert.True(pngBytes.Length > 8);
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal(0x50, pngBytes[1]);
        Assert.Equal(0x4E, pngBytes[2]);
        Assert.Equal(0x47, pngBytes[3]);
    }

    [Fact]
    public void SmtpEmailService_BuildMimeMessage_Converts_DataUri_To_Inline_Cid_LinkedResource()
    {
        var options = new SmtpOptions
        {
            FromEmail = "no-reply@sukienbanve.id.vn",
            FromName = "Ticketing Platform"
        };

        var integrationEvent = new TicketIssuedIntegrationEvent
        {
            OrderId = Guid.NewGuid(),
            TargetEventId = Guid.NewGuid(),
            EventTitle = "Midnight Jazz",
            CustomerEmail = "customer@example.com",
            CustomerName = "Minh Anh",
            Tickets =
            [
                new IssuedTicketDto
                {
                    TicketId = Guid.NewGuid(),
                    TicketCode = "TCK-MIME-01",
                    TicketTypeName = "VIP",
                    Price = 590000m,
                    QrPayload = "QR-MIME-01"
                }
            ]
        };

        var html = TicketEmailRenderer.RenderTicketIssuedHtml(integrationEvent);
        var message = SmtpEmailService.BuildMimeMessage(options, "customer@example.com", "Vé của bạn", html);

        Assert.Equal("Vé của bạn", message.Subject);
        Assert.Contains(message.From.Mailboxes, m => m.Address == "no-reply@sukienbanve.id.vn");
        Assert.Contains(message.To.Mailboxes, m => m.Address == "customer@example.com");

        var allParts = message.BodyParts.ToList();
        var htmlPart = allParts.OfType<TextPart>().FirstOrDefault(p => p.IsHtml);
        var imagePart = allParts.OfType<MimePart>().FirstOrDefault(p => p.ContentType.IsMimeType("image", "png"));

        Assert.NotNull(htmlPart);
        Assert.NotNull(imagePart);
        Assert.False(string.IsNullOrWhiteSpace(imagePart!.ContentId));
        Assert.Equal(ContentDisposition.Inline, imagePart.ContentDisposition?.Disposition);
        Assert.Contains($"cid:{imagePart.ContentId}", htmlPart!.Text);
        Assert.DoesNotContain("data:image/png;base64,", htmlPart.Text);
    }

    [Fact]
    public async Task EmailRetryWorker_Retries_Failed_Notification_And_Marks_Sent()
    {
        var fakeEmail = new FlakyEmailService(failuresBeforeSuccess: 1);
        var smtpOptions = Options.Create(new SmtpOptions
        {
            Enabled = true,
            Host = "localhost",
            EnableRetryWorker = false,
            MaxRetryAttempts = 3
        });

        var services = new ServiceCollection();
        services.AddScoped(_ => new NotificationsDbContext(_options));
        services.AddSingleton<IEmailService>(fakeEmail);
        using var provider = services.BuildServiceProvider();

        var integrationEvent = new TicketIssuedIntegrationEvent
        {
            OrderId = Guid.NewGuid(),
            TargetEventId = Guid.NewGuid(),
            EventTitle = "Saigon Indie Live",
            CustomerEmail = "retry@example.com",
            CustomerName = "Hoàng Nam",
            Tickets =
            [
                new IssuedTicketDto
                {
                    TicketId = Guid.NewGuid(),
                    TicketCode = "TCK-RETRY-01",
                    TicketTypeName = "Standard",
                    Price = 290000m,
                    QrPayload = "QR-RETRY-01"
                }
            ]
        };

        // Lần 1: Gửi thất bại -> lưu trạng thái Failed
        using (var context = new NotificationsDbContext(_options))
        {
            var handler = new TicketIssuedEventHandler(
                context,
                fakeEmail,
                NullLogger<TicketIssuedEventHandler>.Instance);
            await handler.HandleAsync(integrationEvent, CancellationToken.None);
        }

        Guid notificationId;
        using (var context = new NotificationsDbContext(_options))
        {
            var saved = await context.EmailNotifications.SingleAsync();
            notificationId = saved.Id;
            Assert.Equal(EmailNotificationStatus.Failed, saved.Status);
            Assert.Equal(1, saved.AttemptCount);
            Assert.NotNull(saved.NextRetryAt);

            // Tua NextRetryAt về quá khứ để đủ điều kiện retry ngay trong test
            saved.MarkFailed(saved.LastError ?? "transient", DateTime.UtcNow.AddSeconds(-5));
            await context.SaveChangesAsync();
        }

        // Lần 2: Worker quét và gửi lại thành công
        var worker = new EmailRetryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            smtpOptions,
            NullLogger<EmailRetryWorker>.Instance);

        var processedCount = await worker.ProcessPendingRetriesAsync(CancellationToken.None);
        Assert.Equal(1, processedCount);

        using (var context = new NotificationsDbContext(_options))
        {
            var updated = await context.EmailNotifications.SingleAsync(x => x.Id == notificationId);
            Assert.Equal(EmailNotificationStatus.Sent, updated.Status);
            Assert.NotNull(updated.SentAt);
            Assert.Null(updated.LastError);
            Assert.Null(updated.NextRetryAt);
        }
    }

    [Fact]
    public void PasswordResetEmailRenderer_Encodes_Html_And_Includes_Otp_And_Expiry()
    {
        var html = PasswordResetEmailRenderer.RenderPasswordResetOtpHtml(
            "<b>Nguyen Van A</b>",
            "482910",
            10);

        Assert.Contains("&lt;b&gt;Nguyen Van A&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>Nguyen Van A</b>", html);
        Assert.Contains("482910", html);
        Assert.Contains("10 phút", html);
    }

    [Fact]
    public async Task NotificationsModuleApi_SendPasswordResetOtpAsync_Dispatches_Email_With_Expected_Subject_And_Otp()
    {
        var capturingEmail = new CapturingEmailService();
        var api = new NotificationsModuleApi(capturingEmail, NullLogger<NotificationsModuleApi>.Instance);

        await api.SendPasswordResetOtpAsync(
            "  user@example.com ",
            "Nguyen Van A",
            "739201",
            10,
            CancellationToken.None);

        Assert.Equal("user@example.com", capturingEmail.LastRecipient);
        Assert.Equal("[Ticketing Platform] Mã xác nhận đặt lại mật khẩu", capturingEmail.LastSubject);
        Assert.NotNull(capturingEmail.LastBody);
        Assert.Contains("739201", capturingEmail.LastBody);
        Assert.Contains("10 phút", capturingEmail.LastBody);
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public string? LastRecipient { get; private set; }
        public string? LastSubject { get; private set; }
        public string? LastBody { get; private set; }

        public Task SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            LastRecipient = toEmail;
            LastSubject = subject;
            LastBody = body;
            return Task.CompletedTask;
        }
    }

    private sealed class FlakyEmailService(int failuresBeforeSuccess) : IEmailService
    {
        private int _calls;

        public Task SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls <= failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Simulated transient SMTP error.");
            }

            return Task.CompletedTask;
        }
    }
}
