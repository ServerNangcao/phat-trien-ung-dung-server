namespace Identity.Internal.Application.DTOs;

internal sealed record LoginRequest(
    string Email,
    string Password);

