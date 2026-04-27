using Starter.Application.Common.Interfaces;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Agent-aware `IExecutionContext` installed via the core `AmbientExecutionContext`
/// holder for the duration of an agent run. Implements hybrid-intersection semantics:
/// when both a chat caller and the agent principal are present, `HasPermission(p)`
/// requires both to hold the permission; when only the agent is present (operational
/// run), only the agent's permissions apply.
///
/// Begin a scope at the top of the agent runtime loop and dispose at the end. Run id
/// is attached after Begin via `AttachRunId` (the runtime knows the principal at scope
/// open but the run id is generated inside the loop body).
/// </summary>
public sealed class AgentExecutionScope : IExecutionContext, IDisposable
{
    private readonly IDisposable _ambientScope;
    private readonly Func<string, bool>? _callerHasPermission;
    private readonly Func<string, bool> _agentHasPermission;

    public Guid? UserId { get; }
    public Guid? AgentPrincipalId { get; }
    public Guid? TenantId { get; }
    public Guid? AgentRunId { get; private set; }
    public bool DangerousActionApprovalGrant => false;

    private AgentExecutionScope(
        Guid? userId,
        Guid agentPrincipalId,
        Guid? tenantId,
        Func<string, bool>? callerHasPermission,
        Func<string, bool> agentHasPermission)
    {
        UserId = userId;
        AgentPrincipalId = agentPrincipalId;
        TenantId = tenantId;
        _callerHasPermission = callerHasPermission;
        _agentHasPermission = agentHasPermission;
        _ambientScope = AmbientExecutionContext.Use(this);
    }

    public static AgentExecutionScope Begin(
        Guid? userId,
        Guid agentPrincipalId,
        Guid? tenantId,
        Func<string, bool>? callerHasPermission,
        Func<string, bool> agentHasPermission)
    {
        ArgumentNullException.ThrowIfNull(agentHasPermission);
        return new AgentExecutionScope(userId, agentPrincipalId, tenantId,
            callerHasPermission, agentHasPermission);
    }

    public void AttachRunId(Guid runId) => AgentRunId = runId;

    public bool HasPermission(string permission)
    {
        if (!_agentHasPermission(permission))
            return false;
        if (_callerHasPermission is null)
            return true;
        return _callerHasPermission(permission);
    }

    public void Dispose() => _ambientScope.Dispose();
}
