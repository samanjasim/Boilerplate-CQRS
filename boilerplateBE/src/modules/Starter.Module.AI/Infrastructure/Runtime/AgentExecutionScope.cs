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
///
/// Plan 5d-2: the scope can also install the AsyncLocal
/// <see cref="CurrentAgentRunContextAccessor"/> ambient value for the run, so scoped
/// services (notably <c>AgentToolDispatcher</c>) can read assistant + conversation
/// linkage when staging pending approvals.
/// </summary>
public sealed class AgentExecutionScope : IExecutionContext, IDisposable
{
    private readonly IDisposable _ambientScope;
    private IDisposable? _runCtxScope;
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

    /// <summary>
    /// Plan 5d-2 overload: in addition to the ambient `IExecutionContext`, also installs
    /// the AsyncLocal `ICurrentAgentRunContextAccessor` ambient value carrying assistant
    /// + conversation/task linkage, so `AgentToolDispatcher` (and any future scoped
    /// service) can read it without explicit threading. The accessor scope is disposed
    /// before the ambient context scope to mirror LIFO acquire order.
    /// </summary>
    internal static AgentExecutionScope Begin(
        Guid? userId,
        Guid agentPrincipalId,
        Guid? tenantId,
        Func<string, bool>? callerHasPermission,
        Func<string, bool> agentHasPermission,
        Guid assistantId,
        string assistantName,
        Guid? conversationId,
        Guid? agentTaskId,
        CurrentAgentRunContextAccessor runCtxAccessor)
    {
        ArgumentNullException.ThrowIfNull(agentHasPermission);
        ArgumentNullException.ThrowIfNull(runCtxAccessor);
        ArgumentNullException.ThrowIfNull(assistantName);

        var scope = new AgentExecutionScope(userId, agentPrincipalId, tenantId,
            callerHasPermission, agentHasPermission);
        scope._runCtxScope = runCtxAccessor.Use(new CurrentAgentRunContextAccessor.RunCtx(
            AssistantId: assistantId,
            AssistantName: assistantName,
            AgentPrincipalId: agentPrincipalId,
            ConversationId: conversationId,
            AgentTaskId: agentTaskId,
            RequestingUserId: userId,
            TenantId: tenantId));
        return scope;
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

    public void Dispose()
    {
        // LIFO: dispose the run-context accessor first (acquired second), then the
        // ambient execution context scope (acquired first in the constructor).
        _runCtxScope?.Dispose();
        _ambientScope.Dispose();
    }
}
