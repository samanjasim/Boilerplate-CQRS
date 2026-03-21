using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.UpdateRole;

internal sealed class UpdateRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateRoleCommand, Result>
{
    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return Result.Failure(RoleErrors.NotFound(request.Id));

        if (role.IsSystemRole)
            return Result.Failure(RoleErrors.SystemRoleCannotBeModified());

        // Tenant ownership check: non-platform users cannot modify roles from other tenants
        if (currentUserService.TenantId is not null && role.TenantId != currentUserService.TenantId)
            return Result.Failure(RoleErrors.NotFound(request.Id));

        var nameExists = await context.Roles
            .AnyAsync(r => r.Name == request.Name.Trim() && r.Id != request.Id, cancellationToken);

        if (nameExists)
            return Result.Failure(RoleErrors.NameAlreadyExists(request.Name));

        role.Update(request.Name.Trim(), request.Description?.Trim());

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
