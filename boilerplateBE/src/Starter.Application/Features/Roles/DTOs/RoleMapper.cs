using Starter.Domain.Identity.Entities;
using Riok.Mapperly.Abstractions;

namespace Starter.Application.Features.Roles.DTOs;

[Mapper]
public static partial class RoleMapper
{
    public static RoleDto ToDto(this Role role)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.IsActive,
            role.CreatedAt,
            role.RolePermissions
                .Where(rp => rp.Permission is not null)
                .Select(rp => rp.Permission!.ToDto())
                .ToList());
    }

    public static PermissionDto ToDto(this Permission permission)
    {
        return new PermissionDto(
            permission.Id,
            permission.Name,
            permission.Description,
            permission.Module,
            permission.IsActive);
    }

    public static IReadOnlyList<RoleDto> ToDtoList(this IEnumerable<Role> roles)
    {
        return roles.Select(r => r.ToDto()).ToList();
    }

    public static IReadOnlyList<PermissionDto> ToDtoList(this IEnumerable<Permission> permissions)
    {
        return permissions.Select(p => p.ToDto()).ToList();
    }

    public static IReadOnlyList<PermissionGroupDto> ToGroupedDtoList(this IEnumerable<Permission> permissions)
    {
        return permissions
            .GroupBy(p => p.Module ?? "General")
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.Select(p => p.ToDto()).ToList()))
            .OrderBy(g => g.Module)
            .ToList();
    }
}
