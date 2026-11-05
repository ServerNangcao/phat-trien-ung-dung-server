using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;
using Ticketing.Tests.Notifications.TestSupport;
namespace Ticketing.Tests.Notifications.Handlers;

public class TicketEmailProcessorTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static TicketIssuedIntegrationEvent ValidEvent(string qrCodeUrl = "") => new()
    {
        EventId = Guid.NewGuid(),
        TicketId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerEmail = "fan@example.com",
        EventName = "Rock Fest 2026",
        EventDate = "2026-12-31",
        TicketType = "VIP",
        QrCodeUrl = qrCodeUrl
    };

    private static (TicketEmailProcessor Processor, FakeEmailRepository Repository, FakeEmailService Email, FakeUnitOfWork UnitOfWork) CreateProcessor()
    {
        var repository = new FakeEmailRepository();
        var unitOfWork = new FakeUnitOfWork(repository);
        var email = new FakeEmailService();
        var processor = new TicketEmailProcessor(new FakeInbox(), repository, email, unitOfWork,
            new FixedClock(Now), NullLogger<TicketEmailProcessor>.Instance);
        return (processor, repository, email, unitOfWork);
    }

    [Fact]
    public async Task Prepare_NewEvent_RecordsInboxAndPendingEmailAtomically()
    {
        var (processor, repository, _, unitOfWork) = CreateProcessor();

        var prepared = await processor.PrepareIncomingAsync(ValidEvent());

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
        var @event = ValidEvent();

        var first = await processor.PrepareIncomingAsync(@event);
        var second = await processor.PrepareIncomingAsync(@event);

        first.IsNew.Should().BeTrue();
        second.IsNew.Should().BeFalse();
        second.Email.Should().BeNull();
    }

    [Fact]
    public async Task Prepare_ConcurrentDuplicateLostUniqueRace_ReportsNotNew()
    {
        var (processor, repository, _, unitOfWork) = CreateProcessor();
        unitOfWork.FailNextCommit("A duplicate record was committed concurrently.");

        var prepared = await processor.PrepareIncomingAsync(ValidEvent());

        prepared.IsNew.Should().BeFalse();
        prepared.Email.Should().BeNull();
        repository.Store.Should().BeEmpty(); // the losing commit must not leak a Pending email
    }

    [Fact]
    public async Task Prepare_WithInvalidEmail_ConsumesEventWithoutEmail()
    {
        var (processor, repository, email, _) = CreateProcessor();

        var prepared = await processor.PrepareIncomingAsync(ValidEvent() with { CustomerEmail = "not-an-email" });

        prepared.IsNew.Should().BeTrue();
        prepared.Email.Should().BeNull();
        email.SendCallCount.Should().Be(0);
        repository.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task Deliver_HealthySmtp_MarksSent()
    {
        var (processor, repository, email, _) = CreateProcessor();
        var prepared = await processor.PrepareIncomingAsync(ValidEvent());

        await processor.DeliverAsync(prepared.Email!);

        email.SendCallCount.Should().Be(1);
        email.Sent[0].To.Should().Be("fan@example.com");
        email.Sent[0].Subject.Should().Contain("Rock Fest 2026");
        prepared.Email!.Status.Should().Be(EmailStatus.Sent);
        prepared.Email.SentAt.Should().Be(Now);
        repository.Updated.Should().Contain(prepared.Email);
    }

    [Fact]
    public async Task Deliver_SmtpFailure_MarksFailedAndSchedulesRetry()
    {
        var (processor, _, email, _) = CreateProcessor();
        var prepared = await processor.PrepareIncomingAsync(ValidEvent());
        email.ThrowOnSend = new Exception("SMTP unavailable");

        await processor.DeliverAsync(prepared.Email!);

        var record = prepared.Email!;
        record.Status.Should().Be(EmailStatus.Failed);
        record.RetryCount.Should().Be(1);
        record.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(10), TimeSpan.FromSeconds(1));
        record.LastError.Should().Contain("SMTP unavailable");
    }

    [Fact]
    public async Task Prepare_WithoutQrCodeUrl_GeneratesQrFallbackInBody()
    {
        var (processor, _, email, _) = CreateProcessor();

        var prepared = await processor.PrepareIncomingAsync(ValidEvent(qrCodeUrl: ""));

        email.ThrowOnSend = null;
        await processor.DeliverAsync(prepared.Email!);
        email.Sent[0].Body.Should().Contain("data:image/png;base64,");
    }

    [Fact]
    public async Task Prepare_WithQrCodeUrl_EmbedsProvidedUrl()
    {
        var (processor, _, email, _) = CreateProcessor();

        var prepared = await processor.PrepareIncomingAsync(ValidEvent(qrCodeUrl: "https://storage.example.com/qr/ticket-1.png"));

        await processor.DeliverAsync(prepared.Email!);
        email.Sent[0].Body.Should().Contain("https://storage.example.com/qr/ticket-1.png");
        email.Sent[0].Body.Should().NotContain("data:image/png;base64,");
    }

    [Fact]
    public async Task Deliver_HtmlEncodesDynamicValues()
    {
        var (processor, _, email, _) = CreateProcessor();

        var prepared = await processor.PrepareIncomingAsync(ValidEvent() with { EventName = "Fest <script>alert(1)</script>" });

        await processor.DeliverAsync(prepared.Email!);
        email.Sent[0].Body.Should().Contain("&lt;script&gt;");
        email.Sent[0].Body.Should().NotContain("<script>alert");
    }

    [Fact]
    public async Task Prepare_WithPricingData_RendersInvoiceSection()
    {
        var (processor, _, email, _) = CreateProcessor();
        var @event = ValidEvent() with
        {
            TotalAmount = 250_000m,
            Items = new[] { new TicketItemDto("VIP", 2, 125_000m) }
        };

        var prepared = await processor.PrepareIncomingAsync(@event);
        await processor.DeliverAsync(prepared.Email!);

        email.Sent[0].Body.Should().Contain("Invoice");
        email.Sent[0].Body.Should().Contain("VIP");
        email.Sent[0].Body.Should().Contain("125,000 VND");
        email.Sent[0].Body.Should().Contain("250,000 VND");
    }

    [Fact]
    public async Task Prepare_WithoutPricingData_OmitsInvoiceSection()
    {
        var (processor, _, email, _) = CreateProcessor();

        var prepared = await processor.PrepareIncomingAsync(ValidEvent());
        await processor.DeliverAsync(prepared.Email!);

        email.Sent[0].Body.Should().NotContain("Invoice"); // contract before the invoice extension
    }

    [Fact]
    public async Task Prepare_InvoiceTicketNamesAreHtmlEncoded()
    {
        var (processor, _, email, _) = CreateProcessor();
        var @event = ValidEvent() with
        {
            TotalAmount = 100_000m,
            Items = new[] { new TicketItemDto("VIP <b>prime</b>", 1, 100_000m) }
        };

        var prepared = await processor.PrepareIncomingAsync(@event);

        prepared.Email!.Body.Should().Contain("&lt;b&gt;prime&lt;/b&gt;");
        prepared.Email.Body.Should().NotContain("<b>prime</b>");
    }
}
