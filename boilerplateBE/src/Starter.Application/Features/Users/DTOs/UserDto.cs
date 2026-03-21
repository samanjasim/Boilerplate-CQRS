namespace Starter.Application.Features.Users.DTOs;

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Status,
    bool EmailConfirmed,
    bool PhoneConfirmed,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string>? Permissions = null,
    Guid? TenantId = null,
    string? TenantName = null,
    bool TwoFactorEnabled = false);
