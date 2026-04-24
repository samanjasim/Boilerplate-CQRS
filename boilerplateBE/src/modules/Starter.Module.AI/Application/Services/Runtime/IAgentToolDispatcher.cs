using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services.Runtime;

internal interface IAgentToolDispatcher
{
    Task<AgentToolDispatchResult> DispatchAsync(
        AiToolCall call,
        ToolResolutionResult tools,
        CancellationToken ct);
}

internal sealed record AgentToolDispatchResult(string Json, bool IsError);
