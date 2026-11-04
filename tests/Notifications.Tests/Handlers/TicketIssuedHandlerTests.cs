using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Notifications.Application.Handlers;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Handlers;

public class TicketIssuedHandlerTests
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

    private static TicketIssuedHandler CreateHandler(FakeEmailRepository repository, FakeEmailService email)
        => new(repository, email, new FixedClock(Now), NullLogger<TicketIssuedHandler>.Instance);

    [Fact]
    public async Task Handle_WithHealthySmtp_SendsEmailAndMarksHistorySent()
    {
        var repository = new FakeEmailRepository();
        var email = new FakeEmailService();
        var handler = CreateHandler(repository, email);

        await handler.Handle(ValidEvent(), CancellationToken.None);

        email.SendCallCount.Should().Be(1);
        email.Sent[0].To.Should().Be("fan@example.com");
        email.Sent[0].Subject.Should().Contain("Rock Fest 2026");
        var record = repository.Updated.Single();
        record.Status.Should().Be(EmailStatus.Sent);
        record.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_WithSmtpFailure_MarksFailedAndSchedulesRetry()
    {
        var repository = new FakeEmailRepository();
        var email = new FakeEmailService { ThrowOnSend = new Exception("SMTP unavailable") };
        var handler = CreateHandler(repository, email);

        await handler.Handle(ValidEvent(), CancellationToken.None);

        var record = repository.Updated.Single();
        record.Status.Should().Be(EmailStatus.Failed);
        record.RetryCount.Should().Be(1);
        record.NextRetryAt.Should().BeCloseTo(Now.AddSeconds(10), TimeSpan.FromSeconds(1));
        record.LastError.Should().Contain("SMTP unavailable");
    }

    [Fact]
    public async Task Handle_WithoutQrCodeUrl_GeneratesQrFallbackInBody()
    {
        var repository = new FakeEmailRepository();
        var email = new FakeEmailService();
        var handler = CreateHandler(repository, email);

        await handler.Handle(ValidEvent(qrCodeUrl: ""), CancellationToken.None);

        email.Sent[0].Body.Should().Contain("data:image/png;base64,");
    }

    [Fact]
    public async Task Handle_WithQrCodeUrl_EmbedsProvidedUrl()
    {
        var repository = new FakeEmailRepository();
        var email = new FakeEmailService();
        var handler = CreateHandler(repository, email);

        await handler.Handle(ValidEvent(qrCodeUrl: "https://storage.example.com/qr/ticket-1.png"), CancellationToken.None);

        email.Sent[0].Body.Should().Contain("https://storage.example.com/qr/ticket-1.png");
        email.Sent[0].Body.Should().NotContain("data:image/png;base64,");
    }

    [Fact]
    public async Task Handle_HtmlEncodesDynamicValues()
    {
        var repository = new FakeEmailRepository();
        var email = new FakeEmailService();
        var handler = CreateHandler(repository, email);

        await handler.Handle(ValidEvent() with { EventName = "Fest <script>alert(1)</script>" }, CancellationToken.None);

        email.Sent[0].Body.Should().Contain("&lt;script&gt;");
        email.Sent[0].Body.Should().NotContain("<script>alert");
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_CreatesNoRecordAndSendsNothing()
    {
        var repository = new FakeEmailRepository();
        var email = new FakeEmailService();
        var handler = CreateHandler(repository, email);

        await handler.Handle(ValidEvent() with { CustomerEmail = "not-an-email" }, CancellationToken.None);

        email.SendCallCount.Should().Be(0);
        repository.Added.Should().BeEmpty();
    }
}
