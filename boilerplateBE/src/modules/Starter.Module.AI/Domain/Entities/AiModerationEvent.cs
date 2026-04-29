using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiModerationEvent : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid? AssistantId { get; private set; }
    public Guid? AgentPrincipalId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? AgentTaskId { get; private set; }
    public Guid? MessageId { get; private set; }
    public ModerationStage Stage { get; private set; }
    public SafetyPreset Preset { get; private set; }
    public ModerationOutcome Outcome { get; private set; }
    public string CategoriesJson { get; private set; } = "{}";
    public ModerationProvider Provider { get; private set; }
    public string? BlockedReason { get; private set; }
    public bool RedactionFailed { get; private set; }
    public int LatencyMs { get; private set; }

    private AiModerationEvent() { }
    private AiModerationEvent(
        Guid id,
        Guid? tenantId,
        Guid? assistantId,
        Guid? agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? messageId,
        ModerationStage stage,
        SafetyPreset preset,
        ModerationOutcome outcome,
        string categoriesJson,
        ModerationProvider provider,
        string? blockedReason,
        bool redactionFailed,
        int latencyMs) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        AgentPrincipalId = agentPrincipalId;
        ConversationId = conversationId;
        AgentTaskId = agentTaskId;
        MessageId = messageId;
        Stage = stage;
        Preset = preset;
        Outcome = outcome;
        CategoriesJson = categoriesJson;
        Provider = provider;
        BlockedReason = blockedReason;
        RedactionFailed = redactionFailed;
        LatencyMs = latencyMs;
    }

    public static AiModerationEvent Create(
        Guid? tenantId,
        Guid? assistantId,
        Guid? agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? messageId,
        ModerationStage stage,
        SafetyPreset preset,
        ModerationOutcome outcome,
        string categoriesJson,
        ModerationProvider provider,
        int latencyMs,
        string? blockedReason = null,
        bool redactionFailed = false)
    {
        if (string.IsNullOrWhiteSpace(categoriesJson)) categoriesJson = "{}";
        return new AiModerationEvent(
            Guid.NewGuid(), tenantId, assistantId, agentPrincipalId,
            conversationId, agentTaskId, messageId, stage, preset, outcome,
            categoriesJson, provider, blockedReason, redactionFailed, latencyMs);
    }
}
