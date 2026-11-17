using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Services;

public class RefundEmailProcessorTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static RefundNotice ValidLatePaymentNotice() => new(
        RefundEmailKind.LatePaymentAutoRefund,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "fan@example.com",
        250_000m,
        "Payment received after the reservation expired (ADR-0003).",
        Now);

    private static RefundNotice ValidRefundProcessedNotice() => new(
        RefundEmailKind.RefundProcessed,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "fan@example.com",
        180_000m,
        "Refund approved after verification",
        Now);

    private static (RefundEmailProcessor Processor, FakeEmailRepository Repository, FakeEmailService Email, FakeUnitOfWork UnitOfWork) CreateProcessor()
    {
        var repository = new FakeEmailRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var email = new FakeEmailService();
        var dispatcher = new EmailDispatcher(repository, email, unitOfWork,
            new FixedClock(Now), NullLogger<EmailDispatcher>.Instance);
        var processor = new RefundEmailProcessor(new FakeInbox(), repository, dispatcher, unitOfWork,
            new FixedClock(Now), NullLogger<RefundEmailProcessor>.Instance);
        return (processor, repository, email, unitOfWork);
    }

    [Fact]
    public async Task Prepare_NewLatePaymentEvent_RecordsInboxAndPendingEmailAtomically()
    {
        var (processor, repository, _, unitOfWork) = CreateProcessor();
        var notice = ValidLatePaymentNotice();

        var prepared = await processor.PrepareAsync(notice);

        prepared.IsNew.Should().BeTrue();
        prepared.Email.Should().NotBeNull();
        prepared.Email!.Status.Should().Be(EmailStatus.Pending);
        unitOfWork.CommitCount.Should().Be(1);
        repository.Store.Should().ContainSingle(); // inbox + history committed together
    }

    [Fact]
    public async Task Prepare_RedeliveredEvent_IsNotNew()
    {
        var (processor, _, _, _) = CreateProcessor();
        var notice = ValidLatePaymentNotice();

        var first = await processor.PrepareAsync(notice);
        var second = await processor.PrepareAsync(notice);

        first.IsNew.Should().BeTrue();
        second.IsNew.Should().BeFalse();
        second.Email.Should().BeNull();
    }

    [Fact]
    public async Task Prepare_ConcurrentDuplicateLostUniqueRace_ReportsNotNew()
    {
        var (processor, repository, _, unitOfWork) = CreateProcessor();
        unitOfWork.FailNextCommit("A duplicate record was committed concurrently.");
        var notice = ValidLatePaymentNotice();

        var prepared = await processor.PrepareAsync(notice);

        prepared.IsNew.Should().BeFalse();
        prepared.Email.Should().BeNull();
        repository.Store.Should().BeEmpty(); // the losing commit must not leak a Pending email
    }

    [Fact]
    public async Task Prepare_WithInvalidEmail_ConsumesEventWithoutEmail()
    {
        var (processor, repository, email, _) = CreateProcessor();
        var notice = ValidLatePaymentNotice() with { CustomerEmail = "not-an-email" };

        var prepared = await processor.PrepareAsync(notice);

        prepared.IsNew.Should().BeTrue();
        prepared.Email.Should().BeNull();
        email.SendCallCount.Should().Be(0);
        repository.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task Prepare_WithoutCustomerEmail_ConsumesEventWithoutEmail()
    {
        var (processor, repository, email, _) = CreateProcessor();
        var notice = ValidLatePaymentNotice() with { CustomerEmail = "" };

        var prepared = await processor.PrepareAsync(notice);

        prepared.IsNew.Should().BeTrue();
        prepared.Email.Should().BeNull();
        email.SendCallCount.Should().Be(0);
        repository.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task Deliver_HealthySmtp_MarksSent()
    {
        var (processor, repository, email, _) = CreateProcessor();
        var notice = ValidRefundProcessedNotice();

        var prepared = await processor.PrepareAsync(notice);

        await processor.DeliverAsync(prepared.Email!);

        email.SendCallCount.Should().Be(1);
        email.Sent[0].To.Should().Be("fan@example.com");
        email.Sent[0].Subject.Should().Contain("refund").And.Contain("processed");
        prepared.Email!.Status.Should().Be(EmailStatus.Sent);
        prepared.Email.SentAt.Should().Be(Now);
        repository.Updated.Should().Contain(prepared.Email);
    }

    [Fact]
    public async Task Deliver_SmtpFailure_MarksFailedAndSchedulesRetry()
    {
        var (processor, _, email, _) = CreateProcessor();
        var notice = ValidLatePaymentNotice();

        var prepared = await processor.PrepareAsync(notice);
        email.ThrowOnSend = new Exception("SMTP unavailable");

        await processor.DeliverAsync(prepared.Email!);

        var record = prepared.Email!;
        record.Status.Should().Be(EmailStatus.Failed);
        record.RetryCount.Should().Be(1);
        record.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(10), TimeSpan.FromSeconds(1));
        record.LastError.Should().Contain("SMTP unavailable");
    }

    [Fact]
    public async Task Prepare_LatePayment_RendersAmountAndReason()
    {
        var (processor, _, email, _) = CreateProcessor();
        var notice = ValidLatePaymentNotice();

        var prepared = await processor.PrepareAsync(notice);
        await processor.DeliverAsync(prepared.Email!);

        email.Sent[0].Body.Should().Contain("250,000 VND");
        email.Sent[0].Body.Should().Contain("expired");
        email.Sent[0].Subject.Should().Contain("expired");
    }

    [Fact]
    public async Task Prepare_RefundProcessed_RendersAmount()
    {
        var (processor, _, email, _) = CreateProcessor();
        var notice = ValidRefundProcessedNotice();

        var prepared = await processor.PrepareAsync(notice);
        await processor.DeliverAsync(prepared.Email!);

        email.Sent[0].Body.Should().Contain("180,000 VND");
        email.Sent[0].Subject.Should().Contain("refund").And.Contain("processed");
    }

    [Fact]
    public async Task Prepare_RefundProcessed_IsIdempotent()
    {
        var (processor, _, _, _) = CreateProcessor();
        var notice = ValidRefundProcessedNotice();

        var first = await processor.PrepareAsync(notice);
        var second = await processor.PrepareAsync(notice);

        first.IsNew.Should().BeTrue();
        second.IsNew.Should().BeFalse();
    }
}