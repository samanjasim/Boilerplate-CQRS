using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Built-in assignee resolution strategies shipped with the Workflow module.
/// Supports: SpecificUser, Role, EntityCreator.
/// </summary>
internal sealed class BuiltInAssigneeProvider(IRoleUserReader roleUserReader) : IAssigneeResolverProvider
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

        return await roleUserReader.GetUserIdsByRoleAsync(roleName, context.TenantId, ct);
    }
}
