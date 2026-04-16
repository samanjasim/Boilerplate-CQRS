using Starter.Domain.Common;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class TriggerRuleIntegrationTarget : BaseEntity
{
    public Guid TriggerRuleId { get; private set; }
    public Guid IntegrationConfigId { get; private set; }
    public string? TargetChannelId { get; private set; }

    private TriggerRuleIntegrationTarget() { }

    public static TriggerRuleIntegrationTarget Create(Guid triggerRuleId,
        Guid integrationConfigId, string? targetChannelId)
    {
        return new TriggerRuleIntegrationTarget
        {
            Id = Guid.NewGuid(),
            TriggerRuleId = triggerRuleId,
            IntegrationConfigId = integrationConfigId,
            TargetChannelId = targetChannelId?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
