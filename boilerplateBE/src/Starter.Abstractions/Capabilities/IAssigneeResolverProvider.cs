namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Pluggable assignee resolution for workflow approval steps. Modules register
/// providers to support org-structure-aware strategies (e.g., "OrgManager",
/// "DepartmentHead"). The Workflow module collects all registered providers
/// via <c>IEnumerable&lt;IAssigneeResolverProvider&gt;</c> and routes by
/// strategy name.
/// </summary>
public interface IAssigneeResolverProvider : ICapability
{
    /// <summary>Strategy names this provider handles.</summary>
    IReadOnlyList<string> SupportedStrategies { get; }

    /// <summary>Resolves assignee user IDs for the given strategy and context.</summary>
    Task<IReadOnlyList<Guid>> ResolveAsync(
        string strategy,
        Dictionary<string, object> parameters,
        WorkflowAssigneeContext context,
        CancellationToken ct = default);
}

/// <summary>Context provided to assignee resolvers for strategy evaluation.</summary>
public sealed record WorkflowAssigneeContext(
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid InitiatorUserId,
    string CurrentState);
