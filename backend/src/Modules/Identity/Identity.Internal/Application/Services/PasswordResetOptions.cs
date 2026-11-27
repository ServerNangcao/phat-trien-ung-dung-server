namespace Identity.Internal.Application.Services;

/// <summary>
/// Cấu hình cho luồng đặt lại mật khẩu bằng OTP qua email.
/// </summary>
internal sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int ExpirationMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public bool ExposeDevOtpInResponse { get; set; }
}
