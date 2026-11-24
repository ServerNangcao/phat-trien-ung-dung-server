namespace Notifications.Internal.Infrastructure;

/// <summary>
/// Cấu hình SMTP gửi email vé điện tử qua MailKit.
/// </summary>
internal sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "no-reply@sukienbanve.id.vn";
    public string FromName { get; set; } = "Ticketing Platform";
    public bool UseStartTls { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 15;
    public bool EnableRetryWorker { get; set; } = true;
    public int RetryIntervalSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 5;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Host);
}
