using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Built-in assignee resolution strategies shipped with the Workflow module.
/// Supports: SpecificUser, Role, EntityCreator.
/// </summary>
internal sealed class BuiltInAssigneeProvider(IApplicationDbContext db) : IAssigneeResolverProvider
{
    public IReadOnlyList<string> SupportedStrategies => ["SpecificUser", "Role", "EntityCreator"];

    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        string strategy,
        Dictionary<string, object> parameters,
        WorkflowAssigneeContext context,
        CancellationToken ct = default)
    {
        return strategy switch
        {
            "SpecificUser" => ResolveSpecificUser(parameters),
            "Role" => await ResolveByRoleAsync(parameters, context, ct),
            "EntityCreator" => [context.InitiatorUserId],
            _ => [],
        };
    }

    private static IReadOnlyList<Guid> ResolveSpecificUser(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("userId", out var rawValue))
            return [];

        var raw = rawValue switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => rawValue?.ToString(),
        };

        if (raw is not null && Guid.TryParse(raw, out var userId))
            return [userId];

        return [];
    }

    private async Task<IReadOnlyList<Guid>> ResolveByRoleAsync(
        Dictionary<string, object> parameters,
        WorkflowAssigneeContext context,
        CancellationToken ct)
    {
        if (!parameters.TryGetValue("roleName", out var rawRoleName))
            return [];

        var roleName = rawRoleName switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => rawRoleName?.ToString(),
        };

        if (string.IsNullOrWhiteSpace(roleName))
            return [];

        // Find the role by name within this tenant (or system roles)
        var role = await db.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.Name == roleName
                && (r.TenantId == context.TenantId || r.IsSystemRole))
            .FirstOrDefaultAsync(ct);

        if (role is null)
            return [];

        // Return all users assigned that role, scoped to the tenant
        var userIds = await db.UserRoles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ur => ur.RoleId == role.Id)
            .Join(db.Users.AsNoTracking().IgnoreQueryFilters()
                    .Where(u => u.TenantId == context.TenantId),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => u.Id)
            .ToListAsync(ct);

        return userIds;
    }
}
