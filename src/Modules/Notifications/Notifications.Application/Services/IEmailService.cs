namespace Ticketing.Modules.Notifications.Application.Services;

/// <summary>
/// Service responsible for sending emails via SMTP/MailKit with retry logic.
/// </summary>
public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
    Task SendAsync(IReadOnlyList<string> to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
}