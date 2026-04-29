using Starter.Application.Common.Interfaces;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// One-shot wrapper installed via <see cref="AmbientExecutionContext.Use"/> for the
/// duration of an approved MediatR re-dispatch. Delegates everything to the inner
/// context but flips <see cref="IExecutionContext.DangerousActionApprovalGrant"/> to true,
/// causing AgentToolDispatcher to bypass the [DangerousAction] check exactly once.
/// </summary>
internal sealed class ApprovalGrantExecutionContext(IExecutionContext inner) : IExecutionContext
{
    public Guid? UserId => inner.UserId;
    public Guid? AgentPrincipalId => inner.AgentPrincipalId;
    public Guid? TenantId => inner.TenantId;
    public Guid? AgentRunId => inner.AgentRunId;
    public bool DangerousActionApprovalGrant => true;
    public bool HasPermission(string permission) => inner.HasPermission(permission);
}
