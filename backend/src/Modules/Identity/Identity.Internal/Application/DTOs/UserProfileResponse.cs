namespace Identity.Internal.Application.DTOs;

internal sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

