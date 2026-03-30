using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IFeatureFlagService featureFlagService) : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;

        // Tenant users need the feature flag enabled to create custom roles
        if (tenantId is not null)
        {
            var customRolesEnabled = await featureFlagService.IsEnabledAsync("roles.tenant_custom_enabled", cancellationToken);
            if (!customRolesEnabled)
                return Result.Failure<Guid>(RoleErrors.CustomRolesDisabled());
        }

        // Check name is unique within the same tenant scope
        var nameExists = await context.Roles
            .AnyAsync(r => r.Name == request.Name.Trim() && r.TenantId == tenantId, cancellationToken);

        if (nameExists)
            return Result.Failure<Guid>(RoleErrors.NameAlreadyExists(request.Name));

        var role = Role.Create(
            request.Name.Trim(),
            request.Description?.Trim(),
            tenantId: tenantId);

        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
