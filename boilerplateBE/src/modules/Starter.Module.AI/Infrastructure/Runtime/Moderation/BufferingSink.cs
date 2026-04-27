using System.Text;
using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Runtime.Moderation;

/// <summary>
/// Holds <see cref="OnDeltaAsync"/> and <see cref="OnAssistantMessageAsync"/> events while
/// the run executes; observability events (step start/complete, tool call/result) pass through
/// immediately. After the run completes the moderation decorator inspects
/// <see cref="BufferedContent"/>, decides allow/block/redact, and either calls
/// <see cref="ReleaseAsync"/> with the (possibly redacted) text or skips release on Block.
/// </summary>
internal sealed class BufferingSink(IAgentRunSink inner) : IAgentRunSink
{
    private readonly StringBuilder _buffer = new();
    private AgentAssistantMessage? _heldMessage;

    public string BufferedContent => _buffer.ToString();

    public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => inner.OnStepStartedAsync(stepIndex, ct);
    public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => inner.OnToolCallAsync(call, ct);
    public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => inner.OnToolResultAsync(result, ct);
    public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => inner.OnStepCompletedAsync(step, ct);

    public Task OnDeltaAsync(string contentDelta, CancellationToken ct)
    {
        _buffer.Append(contentDelta);
        return Task.CompletedTask;
    }

    public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct)
    {
        _heldMessage = message;
        return Task.CompletedTask;
    }

    public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) =>
        inner.OnRunCompletedAsync(result, ct);

    /// <summary>Flush held content (after moderation passes / redaction completes).</summary>
    public async Task ReleaseAsync(string content, CancellationToken ct)
    {
        if (_heldMessage is { } msg)
            await inner.OnAssistantMessageAsync(msg with { Content = content }, ct);
        if (!string.IsNullOrEmpty(content))
            await inner.OnDeltaAsync(content, ct);
    }
}
