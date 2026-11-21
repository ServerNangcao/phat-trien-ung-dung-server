using System.Net.Sockets;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Notifications.Infrastructure.Email;
using Ticketing.Tests.Notifications.TestSupport;

namespace Ticketing.Tests.Notifications.Infrastructure;

/// <summary>
/// Drives the real MailKit adapter against an in-process SMTP server, covering the
/// boundary that the FakeEmailService tests cannot reach: EHLO/AUTH/DATA negotiation
/// and the transient-retry loop. Auth values are generated at runtime so no credential
/// material exists in source.
/// </summary>
public class SmtpEmailServiceTests
{
    private static SmtpEmailService CreateService(FakeSmtpServer server) => new(
        Options.Create(new SmtpOptions
        {
            Host = "localhost",
            Port = server.Port,
            User = GenerateToken(),
            Password = GenerateToken(),
            From = "no-reply@ticketing.local",
            UseSsl = false,
            RetryBaseDelaySeconds = 0 // instant retries in tests
        }),
        NullLogger<SmtpEmailService>.Instance);

    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    [Fact]
    public async Task SendAsync_DeliversMessageThroughRealSmtpDialogue()
    {
        using var server = new FakeSmtpServer();
        server.Start();
        var service = CreateService(server);

        try
        {
            await service.SendAsync("fan@example.com", "Ticket confirmation", "<p>Your ticket</p>", true);

            server.ReceivedMessages.Should().HaveCount(1);
            var message = server.ReceivedMessages[0];
            message.Should().Contain("fan@example.com"); // RCPT TO / recipient headers
            message.Should().Contain("Subject: Ticket confirmation");
            message.Should().Contain("Your ticket");
        }
        catch (Exception)
        {
            throw new Exception("SMTP dialogue transcript: " + string.Join(" | ", server.Transcript));
        }
    }

    [Fact]
    public async Task SendAsync_WithTransientServerFailure_IsRetriedAndDelivered()
    {
        using var server = new FakeSmtpServer { TransientFailuresBeforeAccept = 1 };
        server.Start();
        var service = CreateService(server);

        await service.SendAsync("fan@example.com", "Ticket confirmation", "<p>Your ticket</p>", true);

        server.MailFromAttempts.Should().Be(2); // first MAIL rejected 451, second accepted
        server.ReceivedMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendAsync_WithPersistentServerFailure_ThrowsAfterThreeAttempts()
    {
        using var server = new FakeSmtpServer { TransientFailuresBeforeAccept = int.MaxValue };
        server.Start();
        var service = CreateService(server);

        var act = async () => await service.SendAsync("fan@example.com", "Ticket confirmation", "<p>x</p>", true);

        await act.Should().ThrowAsync<Exception>();
        server.MailFromAttempts.Should().Be(3); // maxAttempts reached, then the error propagates
    }

    [Fact]
    public async Task SendAsync_SendsToAllRecipientsInBatchOverload()
    {
        using var server = new FakeSmtpServer();
        server.Start();
        var service = CreateService(server);

        await service.SendAsync(new[] { "a@example.com", "b@example.com" }, "Batch", "<p>x</p>", true);

        server.ReceivedMessages.Should().HaveCount(1);
        var message = server.ReceivedMessages[0];
        message.Should().Contain("a@example.com");
        message.Should().Contain("b@example.com");
    }
}
