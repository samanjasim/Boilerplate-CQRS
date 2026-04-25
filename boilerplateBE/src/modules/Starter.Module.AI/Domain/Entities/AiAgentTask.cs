using Starter.Domain.Common;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAgentTask : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Instruction { get; private set; } = default!;
    public AgentTaskStatus Status { get; private set; }
    public string Steps { get; private set; } = "[]";
    public string? Result { get; private set; }
    public int TotalTokensUsed { get; private set; }
    public int StepCount { get; private set; }
    public AgentTriggerSource TriggeredBy { get; private set; }
    public Guid? TriggerId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AiAgentTask() { }

    private AiAgentTask(
        Guid id,
        Guid? tenantId,
        Guid assistantId,
        Guid userId,
        string instruction,
        AgentTriggerSource triggeredBy,
        Guid? triggerId) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        UserId = userId;
        Instruction = instruction;
        Status = AgentTaskStatus.Queued;
        TriggeredBy = triggeredBy;
        TriggerId = triggerId;
    }

    public static AiAgentTask Create(
        Guid? tenantId,
        Guid assistantId,
        Guid userId,
        string instruction,
        AgentTriggerSource triggeredBy = AgentTriggerSource.User,
        Guid? triggerId = null)
    {
        return new AiAgentTask(
            Guid.NewGuid(),
            tenantId,
            assistantId,
            userId,
            instruction.Trim(),
            triggeredBy,
            triggerId);
    }

    public void MarkRunning()
    {
        Status = AgentTaskStatus.Running;
        StartedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void AddStep(string stepsJson, int inputTokens, int outputTokens)
    {
        Steps = stepsJson;
        StepCount++;
        TotalTokensUsed += inputTokens + outputTokens;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(string result)
    {
        Status = AgentTaskStatus.Completed;
        Result = result;
        CompletedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AgentTaskStatus.Failed;
        Result = errorMessage;
        CompletedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        Status = AgentTaskStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
