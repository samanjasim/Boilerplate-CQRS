using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Identity;

/// <summary>
/// Resolves the union of permissions for an agent principal by joining
/// `AiAgentRole` (AI module) with core `Role`, `RolePermission`, and `Permission`.
/// The cross-context query is necessary because role-permission data lives in core;
/// the AI module owns only the agent-to-role mapping.
/// </summary>
internal sealed class AgentPermissionResolver(
    IApplicationDbContext appDb,
    AiDbContext aiDb) : IAgentPermissionResolver
{
    public async Task<HashSet<string>> GetPermissionsAsync(Guid agentPrincipalId, CancellationToken ct = default)
    {
        var roleIds = await aiDb.AiAgentRoles
            .AsNoTracking()
            .Where(r => r.AgentPrincipalId == agentPrincipalId)
            .Select(r => r.RoleId)
            .ToListAsync(ct);

        if (roleIds.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Cross-context read: ignore filters because seeders/system flows may run without
        // a tenant context, and Permission/RolePermission are platform-global anyway.
        var permissions = await appDb.RolePermissions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission!.Name)
            .ToListAsync(ct);

        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
