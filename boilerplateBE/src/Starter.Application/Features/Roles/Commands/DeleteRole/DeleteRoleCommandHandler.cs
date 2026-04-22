using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Roles.Commands.DeleteRole;

internal sealed class DeleteRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IResourceAccessService accessService) : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return Result.Failure(RoleErrors.NotFound(request.Id));

        if (role.IsSystemRole)
            return Result.Failure(RoleErrors.SystemRoleCannotBeDeleted());

        // Tenant ownership check: non-platform users cannot delete roles from other tenants
        if (currentUserService.TenantId is not null && role.TenantId != currentUserService.TenantId)
            return Result.Failure(RoleErrors.NotFound(request.Id));

        if (role.UserRoles.Count > 0)
            return Result.Failure(RoleErrors.RoleInUse(role.Name));

        var roleGrants = await context.ResourceGrants
            .Where(g => g.SubjectType == GrantSubjectType.Role && g.SubjectId == role.Id)
            .ToListAsync(cancellationToken);
        context.ResourceGrants.RemoveRange(roleGrants);

        await accessService.InvalidateRoleMembersAsync(role.Id, cancellationToken);

        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
