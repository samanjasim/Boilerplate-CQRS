using Starter.Module.AI.Application.Services;
using Starter.Abstractions.Ai;
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
    bool Streaming = false,
    PersonaContext? Persona = null,
    // Plan 5d-1: identifies which assistant + tenant this run belongs to, plus the human
    // chat caller for hybrid-intersection security. AssistantId may be null in tests or
    // legacy callers; CallerUserId is null for operational (event/cron) agent runs.
    Guid? AssistantId = null,
    Guid? TenantId = null,
    Guid? CallerUserId = null,
    Func<string, bool>? CallerHasPermission = null,
    // Plan 5d-2: assistant display name + conversation/task linkage threaded through
    // to AgentExecutionScope so AsyncLocal `ICurrentAgentRunContextAccessor` can be set
    // for the duration of the run (consumed by AgentToolDispatcher when staging
    // pending approvals). Null on legacy callers / tests without an assistant.
    string? AssistantName = null,
    Guid? ConversationId = null,
    Guid? AgentTaskId = null);

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
