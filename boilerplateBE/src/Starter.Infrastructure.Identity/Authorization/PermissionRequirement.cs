using Microsoft.AspNetCore.Authorization;

namespace Starter.Infrastructure.Identity.Authorization;

/// <summary>
/// Requirement for permission-based authorization.
/// Supports pipe-delimited "any-of" semantics, e.g. "ApiKeys.View|ApiKeys.ViewPlatform".
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }

    public PermissionRequirement(string permission)
    {
        Permissions = permission.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
