using Ticketing.Modules.Notifications.Application.Services;

namespace Ticketing.Tests.Notifications.TestSupport;

/// <summary>
/// Records sends instead of contacting an SMTP server. Set ThrowOnSend to simulate
/// SMTP failures for the retry paths.
/// </summary>
public sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Exception? ThrowOnSend { get; set; }

    public int SendCallCount => Sent.Count;

    public Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }

    public Task SendAsync(IReadOnlyList<string> to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        foreach (var recipient in to)
            Sent.Add((recipient, subject, body));

        return Task.CompletedTask;
    }
}
