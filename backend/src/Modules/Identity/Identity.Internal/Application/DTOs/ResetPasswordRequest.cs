namespace Identity.Internal.Application.DTOs;

internal sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
