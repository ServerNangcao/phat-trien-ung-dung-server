using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Email;
using Ticketing.Modules.Notifications.Infrastructure.Repositories;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Services;

public class EmailRetryProcessorTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static (EmailRetryProcessor Processor, EmailRepository Repository, NotificationsDbContext Db) CreateProcessor(FakeEmailService email)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new EmailRepository(db);
        var processor = new EmailRetryProcessor(repository, email, NullLogger<EmailRetryProcessor>.Instance);
        return (processor, repository, db);
    }

    private static async Task<EmailHistory> SeedFailedEmailAsync(EmailRepository repository, int retryCount = 1, int retryInMinutesAgo = 1)
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now.AddMinutes(-5)).Value;
        for (var i = 0; i < retryCount; i++)
            email.MarkFailed("SMTP unavailable");
        email.ScheduleRetry(Now.AddMinutes(-retryInMinutesAgo)); // backoff already elapsed
        await repository.AddAsync(email);
        return email;
    }

    [Fact]
    public async Task ProcessDue_DueFailedEmail_IsResentAndMarkedSent()
    {
        var email = new FakeEmailService();
        var (processor, repository, db) = CreateProcessor(email);
        var seeded = await SeedFailedEmailAsync(repository);

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(1);
        var stored = await db.EmailHistories.FirstAsync(e => e.Id == seeded.Id);
        stored.Status.Should().Be(EmailStatus.Sent);
        stored.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task ProcessDue_WhenRetryStillFails_SchedulesNextBackoff()
    {
        var email = new FakeEmailService { ThrowOnSend = new Exception("SMTP down") };
        var (processor, repository, db) = CreateProcessor(email);
        var seeded = await SeedFailedEmailAsync(repository, retryCount: 1);

        await processor.ProcessDueAsync(Now);

        var stored = await db.EmailHistories.FirstAsync(e => e.Id == seeded.Id);
        stored.Status.Should().Be(EmailStatus.Failed);
        stored.RetryCount.Should().Be(2);
        stored.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(20), TimeSpan.FromSeconds(1)); // 10s × attempt 2
    }

    [Fact]
    public async Task ProcessDue_ExhaustedEmails_AreNotRetried()
    {
        var email = new FakeEmailService();
        var (processor, repository, _) = CreateProcessor(email);
        await SeedFailedEmailAsync(repository, retryCount: EmailRetryProcessor.MaxRetries);

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessDue_PendingEmailOlderThan30s_IsRecoveredAfterCrash()
    {
        var email = new FakeEmailService();
        var (processor, repository, db) = CreateProcessor(email);
        var abandoned = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now.AddSeconds(-40)).Value;
        await repository.AddAsync(abandoned); // persisted, then the process crashed before sending

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(1);
        (await db.EmailHistories.FirstAsync(e => e.Id == abandoned.Id)).Status.Should().Be(EmailStatus.Sent);
    }

    [Fact]
    public async Task ProcessDue_RecentPendingEmail_IsNotPickedUp()
    {
        var email = new FakeEmailService();
        var (processor, repository, _) = CreateProcessor(email);
        var fresh = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now.AddSeconds(-5)).Value;
        await repository.AddAsync(fresh);

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(0);
    }
}
