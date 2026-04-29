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
        ValidateTenantId(tenantId);
        ValidateValues(model, maxTokens, temperature);

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
        ValidateValues(model, maxTokens, temperature);

        Provider = provider;
        Model = model.Trim();
        MaxTokens = maxTokens;
        Temperature = temperature;
        ModifiedAt = DateTime.UtcNow;
    }

    private static void ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
    }

    private static void ValidateValues(string model, int? maxTokens, double? temperature)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model must not be blank.", nameof(model));

        if (maxTokens is < 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Max tokens must not be negative.");

        if (temperature is null)
            return;

        var value = temperature.Value;
        if (!double.IsFinite(value) || value is < 0.0 or > 2.0)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0.0 and 2.0.");
    }
}
