using Starter.Application.Features.Users.DTOs;

namespace Starter.Application.Features.Auth.DTOs;

public sealed record LoginResponseDto(
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    UserDto? User,
    bool RequiresTwoFactor = false);
