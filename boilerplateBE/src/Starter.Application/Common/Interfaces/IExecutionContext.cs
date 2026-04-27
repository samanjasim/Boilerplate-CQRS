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
    bool HasPermission(string permission);
}
