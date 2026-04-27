namespace Starter.Application.Common.Interfaces;

/// <summary>
/// Resolves the union of permissions held by all roles assigned to an agent principal
/// (via `AiAgentRole`). Implementation joins core `Role` → `RolePermission` → `Permission`.
/// Per-run callers should cache the result for the duration of the agent run; role
/// changes mid-run intentionally do not take effect until the next run (acceptable
/// trade-off, documented in spec §11).
/// </summary>
public interface IAgentPermissionResolver
{
    Task<HashSet<string>> GetPermissionsAsync(Guid agentPrincipalId, CancellationToken ct = default);
}
