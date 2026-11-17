namespace Identity.Internal.Application.DTOs;

internal sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

