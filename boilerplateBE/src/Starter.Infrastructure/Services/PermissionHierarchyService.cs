using Starter.Application.Common.Interfaces;
using Starter.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Starter.Infrastructure.Services;

internal sealed class PermissionHierarchyService(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IPermissionHierarchyService
{
    public async Task<bool> CanAssignRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (currentUserService.IsInRole(Roles.SuperAdmin))
            return true;

        var currentPermissions = await GetCurrentUserPermissionNamesAsync(cancellationToken);

        var targetPermissionNames = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission!.Name)
            .ToListAsync(cancellationToken);

        var roleExists = await context.Roles
            .AnyAsync(r => r.Id == roleId, cancellationToken);

        if (!roleExists)
            return false;

        return targetPermissionNames.All(tp => currentPermissions.Contains(tp));
    }

    public async Task<bool> ArePermissionsWithinCeilingAsync(
        IEnumerable<Guid> permissionIds,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.IsInRole(Roles.SuperAdmin))
            return true;

        var currentPermissions = await GetCurrentUserPermissionNamesAsync(cancellationToken);

        var targetPermissionNames = await context.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return targetPermissionNames.All(tp => currentPermissions.Contains(tp));
    }

    public Task<HashSet<string>> GetCurrentUserPermissionNamesAsync(CancellationToken cancellationToken = default)
    {
        var permissionNames = currentUserService.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(permissionNames);
    }
}
