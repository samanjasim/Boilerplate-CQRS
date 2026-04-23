using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAgentTrigger : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TriggerType TriggerType { get; private set; }
    public string? CronExpression { get; private set; }
    public string? EventType { get; private set; }
    public string Instruction { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public DateTime? NextRunAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private AiAgentTrigger() { }

    private AiAgentTrigger(
        Guid id,
        Guid? tenantId,
        Guid assistantId,
        string name,
        string? description,
        TriggerType triggerType,
        string? cronExpression,
        string? eventType,
        string instruction,
        bool isActive,
        Guid createdByUserId) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        Name = name;
        Description = description;
        TriggerType = triggerType;
        CronExpression = cronExpression;
        EventType = eventType;
        Instruction = instruction;
        IsActive = isActive;
        CreatedByUserId = createdByUserId;
    }

    public static AiAgentTrigger CreateCron(
        Guid? tenantId,
        Guid assistantId,
        string name,
        string instruction,
        string cronExpression,
        Guid createdByUserId,
        string? description = null,
        bool isActive = true)
    {
        return new AiAgentTrigger(
            Guid.NewGuid(),
            tenantId,
            assistantId,
            name.Trim(),
            description?.Trim(),
            TriggerType.Cron,
            cronExpression.Trim(),
            null,
            instruction.Trim(),
            isActive,
            createdByUserId);
    }

    public static AiAgentTrigger CreateEvent(
        Guid? tenantId,
        Guid assistantId,
        string name,
        string instruction,
        string eventType,
        Guid createdByUserId,
        string? description = null,
        bool isActive = true)
    {
        return new AiAgentTrigger(
            Guid.NewGuid(),
            tenantId,
            assistantId,
            name.Trim(),
            description?.Trim(),
            TriggerType.DomainEvent,
            null,
            eventType.Trim(),
            instruction.Trim(),
            isActive,
            createdByUserId);
    }

    public void UpdateLastRun()
    {
        LastRunAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetNextRun(DateTime nextRunAt)
    {
        NextRunAt = nextRunAt;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Toggle(bool active)
    {
        IsActive = active;
        ModifiedAt = DateTime.UtcNow;
    }
}
