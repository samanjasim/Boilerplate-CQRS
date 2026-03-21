using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.AssignUserRole;

internal sealed class AssignUserRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<AssignUserRoleCommand, Result>
{
    public async Task<Result> Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
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

        if (user.UserRoles.Any(ur => ur.RoleId == role.Id))
            return Result.Failure(UserErrors.RoleAlreadyAssigned(role.Name));

        user.AddRole(role, currentUserService.UserId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
