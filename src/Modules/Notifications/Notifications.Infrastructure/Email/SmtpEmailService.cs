using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SMTP configuration for email sending.
/// </summary>
public sealed class SmtpOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? User { get; init; }
    public string? Password { get; init; }
    public string? From { get; init; }
    public bool UseSsl { get; init; } = true;
}

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> options, IClock clock, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From ?? "noreply@example.com"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        await SendAsyncInternal(message, cancellationToken);
    }

    public async Task SendAsync(IReadOnlyList<string> to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From ?? "noreply@example.com"));

        foreach (var recipient in to)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        await SendAsyncInternal(message, cancellationToken);
    }

    private async Task SendAsyncInternal(MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(_options.Host ?? "smtp.example.com", _options.Port, _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
            await client.AuthenticateAsync(_options.User ?? "", _options.Password ?? "", cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}