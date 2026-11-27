namespace Notifications.Contracts;

/// <summary>
/// API công khai của module Notifications.
/// Các module khác gọi qua interface này (in-process).
/// </summary>
public interface INotificationsModuleApi
{
    /// <summary>
    /// Gửi email chứa mã OTP 6 chữ số để xác nhận đặt lại mật khẩu.
    /// </summary>
    Task SendPasswordResetOtpAsync(
        string toEmail,
        string fullName,
        string otpCode,
        int expiresInMinutes,
        CancellationToken cancellationToken = default);
}
