using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// AsyncLocal-backed implementation of <see cref="ICurrentAgentRunContextAccessor"/>. The
/// runtime opens a scope at the top of the agent loop via <see cref="Use(RunCtx)"/>; the
/// returned <see cref="IDisposable"/> restores the previous value when disposed, making this
/// safe for nested or re-entrant runs.
/// </summary>
internal sealed class CurrentAgentRunContextAccessor : ICurrentAgentRunContextAccessor
{
    private static readonly AsyncLocal<RunCtx?> _current = new();

    public Guid? AssistantId => _current.Value?.AssistantId;
    public string? AssistantName => _current.Value?.AssistantName;
    public Guid? AgentPrincipalId => _current.Value?.AgentPrincipalId;
    public Guid? ConversationId => _current.Value?.ConversationId;
    public Guid? AgentTaskId => _current.Value?.AgentTaskId;
    public Guid? RequestingUserId => _current.Value?.RequestingUserId;
    public Guid? TenantId => _current.Value?.TenantId;

    public IDisposable Use(RunCtx ctx)
    {
        var prev = _current.Value;
        _current.Value = ctx;
        return new Restorer(prev);
    }

    internal sealed record RunCtx(
        Guid AssistantId,
        string AssistantName,
        Guid AgentPrincipalId,
        Guid? ConversationId,
        Guid? AgentTaskId,
        Guid? RequestingUserId,
        Guid? TenantId);

    private sealed class Restorer(RunCtx? prev) : IDisposable
    {
        public void Dispose() => _current.Value = prev;
    }
}
