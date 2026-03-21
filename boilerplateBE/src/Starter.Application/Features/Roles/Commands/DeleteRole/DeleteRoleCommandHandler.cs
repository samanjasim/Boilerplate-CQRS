using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.DeleteRole;

internal sealed class DeleteRoleCommandHandler(
    IApplicationDbContext context) : IRequestHandler<DeleteRoleCommand, Result>
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

        if (role.UserRoles.Count > 0)
            return Result.Failure(RoleErrors.RoleInUse(role.Name));

        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
