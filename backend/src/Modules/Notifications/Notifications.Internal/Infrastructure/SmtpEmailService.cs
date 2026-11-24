using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace Notifications.Internal.Infrastructure;

/// <summary>
/// Dịch vụ gửi email qua giao thức SMTP (MailKit).
/// Tự động tách ảnh data:image/png;base64 thành inline LinkedResources (cid:) để tương thích Gmail/Outlook.
/// </summary>
internal sealed partial class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    [GeneratedRegex(@"src\s*=\s*""data:image/(?<mime>png|jpeg|jpg);base64,(?<data>[A-Za-z0-9+/=]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineDataImageRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("SMTP chưa được cấu hình (Smtp:Enabled/Smtp:Host).");
        }

        var message = BuildMimeMessage(_options, toEmail, subject, body);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120)));

        var socketOptions = _options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, timeoutCts.Token);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, timeoutCts.Token);
        }

        await client.SendAsync(message, timeoutCts.Token);
        await client.DisconnectAsync(true, timeoutCts.Token);

        _logger.LogInformation("Sent SMTP ticket email to {Recipient}: {Subject}", toEmail, subject);
    }

    internal static MimeMessage BuildMimeMessage(
        SmtpOptions options,
        string toEmail,
        string subject,
        string htmlBody)
    {
        var message = new MimeMessage();
        var fromEmail = string.IsNullOrWhiteSpace(options.FromEmail) ? "no-reply@sukienbanve.id.vn" : options.FromEmail.Trim();
        message.From.Add(new MailboxAddress(options.FromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            TextBody = BuildPlainTextFallback(htmlBody)
        };

        var processedHtml = InlineDataImageRegex().Replace(htmlBody, match =>
        {
            var mimeSubType = string.Equals(match.Groups["mime"].Value, "png", StringComparison.OrdinalIgnoreCase)
                ? "png"
                : "jpeg";
            var base64 = match.Groups["data"].Value;
            var imageBytes = Convert.FromBase64String(base64);

            var contentId = MimeUtils.GenerateMessageId();
            var resource = builder.LinkedResources.Add($"qr-{contentId}.{mimeSubType}", imageBytes, new ContentType("image", mimeSubType));
            resource.ContentId = contentId;
            resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);

            return $"src=\"cid:{contentId}\"";
        });

        builder.HtmlBody = processedHtml;
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static string BuildPlainTextFallback(string htmlBody)
    {
        var stripped = HtmlTagRegex().Replace(htmlBody, " ");
        return System.Net.WebUtility.HtmlDecode(stripped).Trim();
    }
}
