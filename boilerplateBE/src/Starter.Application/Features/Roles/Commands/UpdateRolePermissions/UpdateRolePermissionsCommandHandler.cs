using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.UpdateRolePermissions;

internal sealed class UpdateRolePermissionsCommandHandler(
    IApplicationDbContext context,
    IPermissionHierarchyService permissionHierarchyService) : IRequestHandler<UpdateRolePermissionsCommand, Result>
{
    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
            return Result.Failure(RoleErrors.NotFound(request.RoleId));

        if (role.IsSystemRole)
            return Result.Failure(RoleErrors.SystemRoleCannotBeModified());

        // Permission ceiling check: requested permissions must be within the caller's own set
        var withinCeiling = await permissionHierarchyService.ArePermissionsWithinCeilingAsync(
            request.PermissionIds, cancellationToken);

        if (!withinCeiling)
            return Result.Failure(RoleErrors.PermissionCeiling());

        var permissions = await context.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var notFoundIds = request.PermissionIds
            .Except(permissions.Select(p => p.Id))
            .ToList();

        if (notFoundIds.Count > 0)
            return Result.Failure(PermissionErrors.NotFound(notFoundIds.First()));

        role.ClearPermissions();

        foreach (var permission in permissions)
        {
            role.AddPermission(permission);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
