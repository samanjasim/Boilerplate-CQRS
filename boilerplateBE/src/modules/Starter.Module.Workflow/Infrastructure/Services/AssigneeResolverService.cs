using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Coordinates assignee resolution across all registered <see cref="IAssigneeResolverProvider"/>
/// instances. Tries the primary strategy, falls back to the configured fallback strategy,
/// and ultimately falls back to tenant admins if all else fails.
/// </summary>
public sealed class AssigneeResolverService(
    IEnumerable<IAssigneeResolverProvider> providers,
    IUserReader userReader,
    ILogger<AssigneeResolverService> logger)
{
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        AssigneeConfig config,
        WorkflowAssigneeContext context,
        CancellationToken ct = default)
    {
        // Try primary strategy
        var result = await TryResolveAsync(config.Strategy, config.Parameters ?? new(), context, ct);
        if (result.Count > 0) return result;

        // Try fallback strategy
        if (config.Fallback is not null)
        {
            result = await TryResolveAsync(
                config.Fallback.Strategy,
                config.Fallback.Parameters ?? new(),
                context,
                ct);
            if (result.Count > 0) return result;
        }

        // Last resort: tenant admin users
        logger.LogWarning(
            "All assignee strategies failed for entity {EntityType}/{EntityId} in tenant {TenantId}. Falling back to tenant admins.",
            context.EntityType,
            context.EntityId,
            context.TenantId);

        return await GetTenantAdminIdsAsync(context.TenantId, ct);
    }

    private async Task<IReadOnlyList<Guid>> TryResolveAsync(
        string strategy,
        Dictionary<string, object> parameters,
        WorkflowAssigneeContext context,
        CancellationToken ct)
    {
        var provider = providers.FirstOrDefault(
            p => p.SupportedStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase));

        if (provider is null)
        {
            logger.LogDebug(
                "No provider found for assignee strategy '{Strategy}'.",
                strategy);
            return [];
        }

        try
        {
            return await provider.ResolveAsync(strategy, parameters, context, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Assignee strategy '{Strategy}' threw an exception; treating as empty result.",
                strategy);
            return [];
        }
    }

    private async Task<IReadOnlyList<Guid>> GetTenantAdminIdsAsync(
        Guid? tenantId,
        CancellationToken ct)
    {
        if (tenantId is null)
            return [];

        // Delegate to the "Role" strategy targeting the built-in Admin role.
        // This re-uses the BuiltInAssigneeProvider which already has IApplicationDbContext.
        var adminContext = new WorkflowAssigneeContext(
            EntityType: string.Empty,
            EntityId: Guid.Empty,
            TenantId: tenantId,
            InitiatorUserId: Guid.Empty,
            CurrentState: string.Empty);

        var adminIds = await TryResolveAsync(
            "Role",
            new Dictionary<string, object> { ["roleName"] = "Admin" },
            adminContext,
            ct);

        if (adminIds.Count == 0)
            return [];

        // Use IUserReader to verify returned users are still active
        var summaries = await userReader.GetManyAsync(adminIds, ct);
        return summaries
            .Where(u => string.Equals(u.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Select(u => u.Id)
            .ToList();
    }
}
