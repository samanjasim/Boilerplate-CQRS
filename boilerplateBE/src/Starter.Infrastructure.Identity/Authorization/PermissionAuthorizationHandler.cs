using Microsoft.AspNetCore.Authorization;

namespace Starter.Infrastructure.Identity.Authorization;

/// <summary>
/// Authorization handler for permission-based authorization.
/// Succeeds if the user holds ANY one of the requirement's permissions (any-of semantics).
/// Checks permissions from JWT claims for performance.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var userPermissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requirement.Permissions.Any(p => userPermissions.Contains(p)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
