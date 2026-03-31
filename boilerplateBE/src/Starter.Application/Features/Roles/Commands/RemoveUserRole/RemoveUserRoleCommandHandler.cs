using System.Data;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using RoleConstants = Starter.Shared.Constants.Roles;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.RemoveUserRole;

internal sealed class RemoveUserRoleCommandHandler(
    IApplicationDbContext context) : IRequestHandler<RemoveUserRoleCommand, Result>
{
    public async Task<Result> Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(request.UserId));

        var role = await context.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
            return Result.Failure(RoleErrors.NotFound(request.RoleId));

        if (!user.UserRoles.Any(ur => ur.RoleId == role.Id))
            return Result.Failure(UserErrors.RoleNotAssigned(role.Name));

        // Protect last SuperAdmin — use serializable transaction for atomicity
        if (role.Name == RoleConstants.SuperAdmin)
        {
            return await context.ExecuteInTransactionAsync(async ct =>
            {
                var superAdminCount = await context.UserRoles
                    .CountAsync(ur => ur.RoleId == role.Id, ct);

                if (superAdminCount <= 1)
                    return Result.Failure(RoleErrors.LastSuperAdmin());

                user.RemoveRole(role.Id);
                await context.SaveChangesAsync(ct);
                return Result.Success();
            }, IsolationLevel.Serializable, cancellationToken);
        }

        user.RemoveRole(role.Id);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
