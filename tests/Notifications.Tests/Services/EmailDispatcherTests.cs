using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Services;

public class EmailDispatcherTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static (EmailDispatcher Dispatcher, FakeEmailRepository Repository, FakeEmailService Email, FakeUnitOfWork UnitOfWork) CreateDispatcher()
    {
        var repository = new FakeEmailRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var email = new FakeEmailService();
        var dispatcher = new EmailDispatcher(repository, email, unitOfWork,
            new FixedClock(Now), NullLogger<EmailDispatcher>.Instance);
        return (dispatcher, repository, email, unitOfWork);
    }

    [Fact]
    public async Task DeliverAsync_SuccessfulSend_MarksSentAndUpdatesRepository()
    {
        var (dispatcher, repository, email, _) = CreateDispatcher();
        var record = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;

        await dispatcher.DeliverAsync(record);

        email.SendCallCount.Should().Be(1);
        record.Status.Should().Be(EmailStatus.Sent);
        record.SentAt.Should().Be(Now);
        record.NextRetryAt.Should().BeNull();
        record.LastError.Should().BeNull();
        repository.Updated.Should().Contain(record);
    }

    [Fact]
    public async Task DeliverAsync_SmtpFailure_MarksFailedSchedulesRetryLinearBackoff()
    {
        var (dispatcher, _, email, _) = CreateDispatcher();
        var record = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        email.ThrowOnSend = new Exception("SMTP unavailable");

        await dispatcher.DeliverAsync(record);

        record.Status.Should().Be(EmailStatus.Failed);
        record.RetryCount.Should().Be(1);
        record.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(10), TimeSpan.FromSeconds(1));
        record.LastError.Should().Contain("SMTP unavailable");
    }

    [Fact]
    public async Task DeliverAsync_SecondFailure_IncrementsRetryCountAndExtendsBackoff()
    {
        var (dispatcher, _, email, _) = CreateDispatcher();
        var record = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        email.ThrowOnSend = new Exception("SMTP down");

        await dispatcher.DeliverAsync(record);
        var firstRetry = record.NextRetryAt!.Value;
        email.ThrowOnSend = new Exception("SMTP still down");

        await dispatcher.DeliverAsync(record);

        record.RetryCount.Should().Be(2);
        record.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(20), TimeSpan.FromSeconds(1));
        record.NextRetryAt.Should().BeAfter(firstRetry);
    }

    [Fact]
    public async Task DeliverAsync_ThirdFailure_CapsBackoffAt60Seconds()
    {
        var (dispatcher, _, email, _) = CreateDispatcher();
        var record = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        email.ThrowOnSend = new Exception("SMTP down");

        await dispatcher.DeliverAsync(record); // 10s
        await dispatcher.DeliverAsync(record); // 20s
        await dispatcher.DeliverAsync(record); // 30s
        await dispatcher.DeliverAsync(record); // 40s
        await dispatcher.DeliverAsync(record); // 50s
        await dispatcher.DeliverAsync(record); // 60s (capped)
        var sixthRetry = record.NextRetryAt!.Value;

        await dispatcher.DeliverAsync(record); // 7th attempt - should still be 60s

        record.NextRetryAt.Should().BeCloseTo(sixthRetry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DeliverAsync_OperationCanceledException_IsRethrown()
    {
        var (dispatcher, _, email, _) = CreateDispatcher();
        var record = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        email.ThrowOnSend = new OperationCanceledException("Cancelled");

        var act = async () => await dispatcher.DeliverAsync(record);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DeliverAsync_CommitsUnitOfWorkOnSuccessAndFailure()
    {
        var (dispatcher, _, _, unitOfWork) = CreateDispatcher();
        var record = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;

        await dispatcher.DeliverAsync(record);

        unitOfWork.CommitCount.Should().Be(1);
    }
}