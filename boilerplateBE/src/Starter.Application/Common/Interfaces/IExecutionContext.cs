namespace Starter.Application.Common.Interfaces;

/// <summary>
/// Abstracts the principal performing the current MediatR send. The default registration
/// (`HttpExecutionContext`) wraps `ICurrentUserService` and exposes the human caller from
/// the HTTP request claims. Inside an agent run, the runtime installs an
/// `AmbientExecutionContext` (AsyncLocal) that exposes the agent principal and applies
/// hybrid-intersection permission semantics: chat caller permissions ∩ agent permissions
/// when both are present; agent permissions only when no caller (operational agent).
/// </summary>
public interface IExecutionContext
{
    Guid? UserId { get; }
    Guid? AgentPrincipalId { get; }
    Guid? TenantId { get; }

    /// <summary>
    /// The current agent run identifier when this execution context represents an
    /// in-flight agent run. Null for HTTP / non-agent contexts.
    /// </summary>
    Guid? AgentRunId { get; }

    /// <summary>
    /// True for the duration of an approved-action re-dispatch (one-shot). AgentToolDispatcher
    /// skips the [DangerousAction] check when this returns true. Default impls return false.
    /// </summary>
    bool DangerousActionApprovalGrant { get; }

    bool HasPermission(string permission);
}
