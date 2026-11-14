namespace Identity.Internal.Application.DTOs;

internal sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

