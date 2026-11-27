namespace Identity.Internal.Application.DTOs;

internal sealed record ForgotPasswordResponse(
    string Message,
    string? ResetToken = null,
    int CooldownSeconds = 60);
