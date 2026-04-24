using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record AgentRunContext(
    IReadOnlyList<AiChatMessage> Messages,
    string SystemPrompt,
    AgentModelConfig ModelConfig,
    ToolResolutionResult Tools,
    int MaxSteps,
    LoopBreakPolicy LoopBreak,
    bool Streaming = false);

internal sealed record AgentModelConfig(
    AiProviderType Provider,
    string Model,
    double Temperature,
    int MaxTokens);

internal sealed record LoopBreakPolicy(
    bool Enabled = true,
    int MaxIdenticalRepeats = 3)
{
    public static LoopBreakPolicy Default => new();
}
