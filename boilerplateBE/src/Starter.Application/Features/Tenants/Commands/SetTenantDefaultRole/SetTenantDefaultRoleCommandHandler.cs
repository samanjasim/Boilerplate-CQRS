using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using SharedRoles = Starter.Shared.Constants.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;

internal sealed class SetTenantDefaultRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPermissionHierarchyService permissionHierarchyService) : IRequestHandler<SetTenantDefaultRoleCommand, Result>
{
    public async Task<Result> Handle(SetTenantDefaultRoleCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.TenantId));

        // Null means "clear the default" — always allowed
        if (request.RoleId is null)
        {
            tenant.SetDefaultRegistrationRole(null);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // Verify the role exists and is accessible to this tenant
        var role = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r =>
                r.Id == request.RoleId.Value &&
                r.IsActive &&
                (r.TenantId == null || r.TenantId == request.TenantId),
                cancellationToken);

        if (role is null)
            return Result.Failure(TenantErrors.DefaultRoleNotFound(request.RoleId.Value));

        // Non-SuperAdmin: can only set a role with permissions within their own ceiling
        if (!currentUserService.IsInRole(SharedRoles.SuperAdmin))
        {
            var canAssign = await permissionHierarchyService.CanAssignRoleAsync(role.Id, cancellationToken);
            if (!canAssign)
                return Result.Failure(RoleErrors.PermissionCeiling());
        }

        tenant.SetDefaultRegistrationRole(role.Id);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
