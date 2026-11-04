using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Data;
using Ticketing.Modules.Notifications.Infrastructure.Email;
using Ticketing.Modules.Notifications.Infrastructure.Repositories;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Services;

public class EmailRetryProcessorTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static (EmailRetryProcessor Processor, EmailRepository Repository, UnitOfWork UnitOfWork) CreateProcessor(FakeEmailService email)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new EmailRepository(db);
        var unitOfWork = new UnitOfWork(db);
        var ticketProcessor = new TicketEmailProcessor(new FakeInbox(), repository, email, unitOfWork,
            new FixedClock(Now), NullLogger<TicketEmailProcessor>.Instance);
        var processor = new EmailRetryProcessor(repository, ticketProcessor, NullLogger<EmailRetryProcessor>.Instance);
        return (processor, repository, unitOfWork);
    }

    private static async Task<EmailHistory> SeedFailedEmailAsync(EmailRepository repository, UnitOfWork unitOfWork, int retryCount = 1)
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now.AddMinutes(-5)).Value;
        for (var i = 0; i < retryCount; i++)
            email.MarkFailed("SMTP unavailable");
        email.ScheduleRetry(Now.AddMinutes(-1)); // backoff already elapsed
        await repository.AddAsync(email);
        await unitOfWork.CommitAsync();
        return email;
    }

    [Fact]
    public async Task ProcessDue_DueFailedEmail_IsResentAndMarkedSent()
    {
        var email = new FakeEmailService();
        var (processor, repository, unitOfWork) = CreateProcessor(email);
        var seeded = await SeedFailedEmailAsync(repository, unitOfWork);

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(1);
        var stored = await repository.GetByIdAsync(seeded.Id);
        stored!.Status.Should().Be(EmailStatus.Sent);
        stored.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task ProcessDue_WhenRetryStillFails_SchedulesNextBackoff()
    {
        var email = new FakeEmailService { ThrowOnSend = new Exception("SMTP down") };
        var (processor, repository, unitOfWork) = CreateProcessor(email);
        var seeded = await SeedFailedEmailAsync(repository, unitOfWork, retryCount: 1);

        await processor.ProcessDueAsync(Now);

        var stored = await repository.GetByIdAsync(seeded.Id);
        stored!.Status.Should().Be(EmailStatus.Failed);
        stored.RetryCount.Should().Be(2);
        stored.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(20), TimeSpan.FromSeconds(1)); // 10s × attempt 2
    }

    [Fact]
    public async Task ProcessDue_ExhaustedEmails_AreNotRetried()
    {
        var email = new FakeEmailService();
        var (processor, repository, unitOfWork) = CreateProcessor(email);
        await SeedFailedEmailAsync(repository, unitOfWork, retryCount: EmailRetryProcessor.MaxRetries);

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessDue_PendingEmailOlderThan30s_IsRecoveredAfterCrash()
    {
        var email = new FakeEmailService();
        var (processor, repository, unitOfWork) = CreateProcessor(email);
        var abandoned = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now.AddSeconds(-40)).Value;
        await repository.AddAsync(abandoned);
        await unitOfWork.CommitAsync(); // persisted, then the process crashed before sending

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(1);
        (await repository.GetByIdAsync(abandoned.Id))!.Status.Should().Be(EmailStatus.Sent);
    }

    [Fact]
    public async Task ProcessDue_RecentPendingEmail_IsNotPickedUp()
    {
        var email = new FakeEmailService();
        var (processor, repository, unitOfWork) = CreateProcessor(email);
        var fresh = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now.AddSeconds(-5)).Value;
        await repository.AddAsync(fresh);
        await unitOfWork.CommitAsync();

        await processor.ProcessDueAsync(Now);

        email.SendCallCount.Should().Be(0);
    }
}
