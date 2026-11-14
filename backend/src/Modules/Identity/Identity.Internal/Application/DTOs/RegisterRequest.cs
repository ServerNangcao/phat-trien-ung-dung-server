namespace Identity.Internal.Application.DTOs;

internal sealed record RegisterRequest(
    string Email,
    string FullName,
    string Password,
    string? PhoneNumber);

