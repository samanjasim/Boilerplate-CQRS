using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiModelDefault : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public AiAgentClass AgentClass { get; private set; }
    public AiProviderType Provider { get; private set; }
    public string Model { get; private set; } = default!;
    public int? MaxTokens { get; private set; }
    public double? Temperature { get; private set; }

    private AiModelDefault() { }

    private AiModelDefault(
        Guid tenantId,
        AiAgentClass agentClass,
        AiProviderType provider,
        string model,
        int? maxTokens,
        double? temperature) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        AgentClass = agentClass;
        Provider = provider;
        Model = model.Trim();
        MaxTokens = maxTokens;
        Temperature = temperature;
    }

    public static AiModelDefault Create(
        Guid tenantId,
        AiAgentClass agentClass,
        AiProviderType provider,
        string model,
        int? maxTokens,
        double? temperature) =>
        new(tenantId, agentClass, provider, model, maxTokens, temperature);

    public void Update(AiProviderType provider, string model, int? maxTokens, double? temperature)
    {
        Provider = provider;
        Model = model.Trim();
        MaxTokens = maxTokens;
        Temperature = temperature;
        ModifiedAt = DateTime.UtcNow;
    }
}
