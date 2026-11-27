using Microsoft.Extensions.Logging;
using Notifications.Contracts;
using Notifications.Internal.Infrastructure;

namespace Notifications.Internal.Application;

/// <summary>
/// Cài đặt in-process cho public API của module Notifications.
/// </summary>
internal sealed class NotificationsModuleApi : INotificationsModuleApi
{
    private const string PasswordResetSubject = "[Ticketing Platform] Mã xác nhận đặt lại mật khẩu";

    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationsModuleApi> _logger;

    public NotificationsModuleApi(
        IEmailService emailService,
        ILogger<NotificationsModuleApi> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendPasswordResetOtpAsync(
        string toEmail,
        string fullName,
        string otpCode,
        int expiresInMinutes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);

        var normalizedRecipient = toEmail.Trim();
        var htmlBody = PasswordResetEmailRenderer.RenderPasswordResetOtpHtml(
            fullName,
            otpCode,
            expiresInMinutes);

        await _emailService.SendEmailAsync(
            normalizedRecipient,
            PasswordResetSubject,
            htmlBody,
            cancellationToken);

        _logger.LogInformation("Dispatched password reset OTP email to {Recipient}", normalizedRecipient);
    }
}
