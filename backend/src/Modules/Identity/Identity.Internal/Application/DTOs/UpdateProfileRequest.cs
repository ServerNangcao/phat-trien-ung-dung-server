namespace Identity.Internal.Application.DTOs;

internal sealed record UpdateProfileRequest(
    string FullName,
    string? PhoneNumber);

