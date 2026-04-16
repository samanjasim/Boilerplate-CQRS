using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class TriggerRule : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string EventName { get; private set; } = default!;
    public Guid MessageTemplateId { get; private set; }
    public string RecipientMode { get; private set; } = default!;
    public string ChannelSequenceJson { get; private set; } = default!;
    public int DelaySeconds { get; private set; }
    public string? ConditionJson { get; private set; }
    public TriggerRuleStatus Status { get; private set; }

    private readonly List<TriggerRuleIntegrationTarget> _integrationTargets = [];
    public IReadOnlyCollection<TriggerRuleIntegrationTarget> IntegrationTargets => _integrationTargets.AsReadOnly();

    private TriggerRule() { }

    private TriggerRule(Guid id, Guid tenantId, string name, string eventName,
        Guid messageTemplateId, string recipientMode, string channelSequenceJson,
        int delaySeconds, string? conditionJson) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        EventName = eventName;
        MessageTemplateId = messageTemplateId;
        RecipientMode = recipientMode;
        ChannelSequenceJson = channelSequenceJson;
        DelaySeconds = delaySeconds;
        ConditionJson = conditionJson;
        Status = TriggerRuleStatus.Active;
    }

    public static TriggerRule Create(Guid tenantId, string name, string eventName,
        Guid messageTemplateId, string recipientMode, string channelSequenceJson,
        int delaySeconds = 0, string? conditionJson = null)
    {
        return new TriggerRule(Guid.NewGuid(), tenantId, name.Trim(), eventName,
            messageTemplateId, recipientMode, channelSequenceJson, delaySeconds, conditionJson);
    }

    public void Update(string name, string eventName, Guid messageTemplateId,
        string recipientMode, string channelSequenceJson, int delaySeconds, string? conditionJson)
    {
        Name = name.Trim();
        EventName = eventName;
        MessageTemplateId = messageTemplateId;
        RecipientMode = recipientMode;
        ChannelSequenceJson = channelSequenceJson;
        DelaySeconds = delaySeconds;
        ConditionJson = conditionJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate() { Status = TriggerRuleStatus.Active; ModifiedAt = DateTime.UtcNow; }
    public void Deactivate() { Status = TriggerRuleStatus.Inactive; ModifiedAt = DateTime.UtcNow; }

    public void AddIntegrationTarget(TriggerRuleIntegrationTarget target) => _integrationTargets.Add(target);
    public void ClearIntegrationTargets() => _integrationTargets.Clear();
}
