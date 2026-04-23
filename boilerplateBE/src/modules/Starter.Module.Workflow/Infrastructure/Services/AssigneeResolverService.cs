using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Infrastructure.Persistence;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Result of assignee resolution, including any delegation swaps.
/// </summary>
public sealed record AssigneeResolveResult(
    IReadOnlyList<Guid> AssigneeIds,
    IReadOnlyDictionary<Guid, Guid> DelegationMap); // delegateId → originalUserId

/// <summary>
/// Coordinates assignee resolution across all registered <see cref="IAssigneeResolverProvider"/>
/// instances. Tries the primary strategy, falls back to the configured fallback strategy,
/// and ultimately falls back to tenant admins if all else fails.
/// After resolution, checks for active delegation rules and swaps assignees transparently.
/// </summary>
public sealed class AssigneeResolverService(
    IEnumerable<IAssigneeResolverProvider> providers,
    IUserReader userReader,
    ILogger<AssigneeResolverService> logger,
    WorkflowDbContext? workflowDbContext = null)
{
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        AssigneeConfig config,
        WorkflowAssigneeContext context,
        CancellationToken ct = default)
    {
        var resolveResult = await ResolveWithDelegationAsync(config, context, ct);
        return resolveResult.AssigneeIds;
    }

    public async Task<AssigneeResolveResult> ResolveWithDelegationAsync(
        AssigneeConfig config,
        WorkflowAssigneeContext context,
        CancellationToken ct = default)
    {
        // Try primary strategy
        var result = await TryResolveAsync(config.Strategy, config.Parameters ?? new(), context, ct);

        // Try fallback strategy
        if (result.Count == 0 && config.Fallback is not null)
        {
            result = await TryResolveAsync(
                config.Fallback.Strategy,
                config.Fallback.Parameters ?? new(),
                context,
                ct);
        }

        // Last resort: tenant admin users
        if (result.Count == 0)
        {
            logger.LogWarning(
                "All assignee strategies failed for entity {EntityType}/{EntityId} in tenant {TenantId}. Falling back to tenant admins.",
                context.EntityType,
                context.EntityId,
                context.TenantId);

            result = await GetTenantAdminIdsAsync(context.TenantId, ct);
        }

        // Apply delegation rules
        return await ApplyDelegationAsync(result, ct);
    }

    private async Task<AssigneeResolveResult> ApplyDelegationAsync(
        IReadOnlyList<Guid> primaryAssignees,
        CancellationToken ct)
    {
        if (workflowDbContext is null || primaryAssignees.Count == 0)
            return new AssigneeResolveResult(primaryAssignees, new Dictionary<Guid, Guid>());

        var now = DateTime.UtcNow;
        var resultList = new List<Guid>(primaryAssignees);
        var delegationMap = new Dictionary<Guid, Guid>();

        foreach (var userId in primaryAssignees.ToList())
        {
            var delegation = await workflowDbContext.DelegationRules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.FromUserId == userId
                    && d.IsActive
                    && d.StartDate <= now
                    && d.EndDate >= now, ct);

            if (delegation is not null)
            {
                resultList.Remove(userId);
                resultList.Add(delegation.ToUserId);
                delegationMap[delegation.ToUserId] = userId;
            }
        }

        return new AssigneeResolveResult(resultList.AsReadOnly(), delegationMap);
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
