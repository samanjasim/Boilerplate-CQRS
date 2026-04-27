using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Identity.Services;

/// <summary>
/// Default `IExecutionContext` implementation: exposes the human caller from the current
/// HTTP request via `ICurrentUserService`. `AgentPrincipalId` is always null here; agents
/// are surfaced via `AmbientExecutionContext` installed inside the AI module's runtime loop.
/// </summary>
public sealed class HttpExecutionContext(ICurrentUserService current) : IExecutionContext
{
    public Guid? UserId => current.UserId;
    public Guid? AgentPrincipalId => null;
    public Guid? TenantId => current.TenantId;
    public Guid? AgentRunId => null;

    public bool HasPermission(string permission) => current.HasPermission(permission);
}
