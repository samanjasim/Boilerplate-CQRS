using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using SharedRoles = Starter.Shared.Constants.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Queries.GetAssignableRoles;

internal sealed class GetAssignableRolesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPermissionHierarchyService permissionHierarchyService) : IRequestHandler<GetAssignableRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(
        GetAssignableRolesQuery request,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentUserService.IsInRole(SharedRoles.SuperAdmin);

        // Determine the target tenant scope
        Guid? targetTenantId;
        if (currentUserService.TenantId is not null)
        {
            // Tenant user — can only assign roles within their tenant
            targetTenantId = currentUserService.TenantId;
        }
        else
        {
            // Platform admin — use the requested tenant or null for platform roles
            targetTenantId = request.TenantId;
        }

        // Load roles with permissions
        var rolesQuery = context.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => r.IsActive);

        // Scope: system roles (TenantId=null) + target tenant's custom roles
        if (targetTenantId is not null)
        {
            rolesQuery = rolesQuery.Where(r => r.TenantId == null || r.TenantId == targetTenantId);
        }
        else
        {
            // Platform-level invite — only system roles (no tenant custom roles)
            rolesQuery = rolesQuery.Where(r => r.TenantId == null);
        }

        var roles = await rolesQuery
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        // SuperAdmin sees everything
        if (isSuperAdmin)
        {
            return Result.Success<IReadOnlyList<RoleDto>>(roles.ToDtoList());
        }

        // Filter: exclude SuperAdmin role and roles with permissions exceeding caller's
        var currentPermissions = await permissionHierarchyService.GetCurrentUserPermissionNamesAsync(cancellationToken);

        var assignableRoles = roles
            .Where(r => r.Name != SharedRoles.SuperAdmin)
            .Where(r =>
            {
                var rolePermNames = r.RolePermissions
                    .Where(rp => rp.Permission is not null)
                    .Select(rp => rp.Permission!.Name);
                return rolePermNames.All(p => currentPermissions.Contains(p));
            })
            .ToList();

        return Result.Success<IReadOnlyList<RoleDto>>(assignableRoles.ToDtoList());
    }
}
