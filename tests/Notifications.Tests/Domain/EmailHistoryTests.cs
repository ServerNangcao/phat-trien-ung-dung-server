using FluentAssertions;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Notifications.Domain;

public class EmailHistoryTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var result = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now);

        result.IsSuccess.Should().BeTrue();
        var email = result.Value;
        email.OrderId.Should().NotBe(Guid.Empty);
        email.To.Should().Be("fan@example.com");
        email.Subject.Should().Be("Subject");
        email.Body.Should().Be("Body");
        email.CreatedAt.Should().Be(Now);
        email.Status.Should().Be(EmailStatus.Pending);
        email.RetryCount.Should().Be(0);
        email.NextRetryAt.Should().BeNull();
        email.SentAt.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyOrderId_Fails()
    {
        var result = EmailHistory.Create(Guid.Empty, "fan@example.com", "Subject", "Body", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Order ID is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Create_InvalidEmail_Fails(string email)
    {
        var result = EmailHistory.Create(Guid.NewGuid(), email, "Subject", "Body", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Recipient");
    }

    [Fact]
    public void Create_EmptySubject_Fails()
    {
        var result = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "", "Body", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Subject is required");
    }

    [Fact]
    public void Create_EmptyBody_Fails()
    {
        var result = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Body is required");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromEmail()
    {
        var result = EmailHistory.Create(Guid.NewGuid(), "  fan@example.com  ", "Subject", "Body", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.To.Should().Be("fan@example.com");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromSubject()
    {
        var result = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "  Subject  ", "Body", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subject.Should().Be("Subject");
    }

    [Fact]
    public void MarkSent_SetsSentAtAndStatus()
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        var sentAt = Now.AddMinutes(5);

        email.MarkSent(sentAt);

        email.Status.Should().Be(EmailStatus.Sent);
        email.SentAt.Should().Be(sentAt);
        email.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCountAndSetsError()
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;

        email.MarkFailed("SMTP connection refused");

        email.Status.Should().Be(EmailStatus.Failed);
        email.RetryCount.Should().Be(1);
        email.LastError.Should().Be("SMTP connection refused");
    }

    [Fact]
    public void MarkFailed_MultipleTimes_IncrementsRetryCount()
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;

        email.MarkFailed("First failure");
        email.MarkFailed("Second failure");
        email.MarkFailed("Third failure");

        email.RetryCount.Should().Be(3);
        email.LastError.Should().Be("Third failure");
    }

    [Fact]
    public void ScheduleRetry_SetsNextRetryAt()
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        var nextRetry = Now.AddSeconds(30);

        email.ScheduleRetry(nextRetry);

        email.NextRetryAt.Should().Be(nextRetry);
    }

    [Fact]
    public void MarkSent_ClearsNextRetryAt()
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        email.ScheduleRetry(Now.AddSeconds(30));

        email.MarkSent(Now.AddMinutes(1));

        email.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void MarkSent_ThenMarkFailed_ResetsRetryCount()
    {
        var email = EmailHistory.Create(Guid.NewGuid(), "fan@example.com", "Subject", "Body", Now).Value;
        email.MarkSent(Now.AddMinutes(5));

        email.MarkFailed("Late failure");

        email.RetryCount.Should().Be(1);
    }
}