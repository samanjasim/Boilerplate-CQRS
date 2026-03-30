namespace Starter.Application.Features.Roles.DTOs;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    DateTime CreatedAt,
    Guid? TenantId,
    IReadOnlyList<PermissionDto> Permissions);

public sealed record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    string? Module,
    bool IsActive);

public sealed record PermissionGroupDto(
    string Module,
    IReadOnlyList<PermissionDto> Permissions);
