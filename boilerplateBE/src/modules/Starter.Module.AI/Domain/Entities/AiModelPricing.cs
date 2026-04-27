using Starter.Abstractions.Ai;
using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiModelPricing : BaseEntity
{
    public AiProviderType Provider { get; private set; }
    public string Model { get; private set; } = default!;
    public decimal InputUsdPer1KTokens { get; private set; }
    public decimal OutputUsdPer1KTokens { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiModelPricing() { }
    private AiModelPricing(Guid id, AiProviderType provider, string model,
        decimal input, decimal output, bool isActive, DateTimeOffset effectiveFrom,
        Guid? createdBy) : base(id)
    {
        Provider = provider; Model = model.Trim();
        InputUsdPer1KTokens = input; OutputUsdPer1KTokens = output;
        IsActive = isActive; EffectiveFrom = effectiveFrom; CreatedByUserId = createdBy;
    }

    public static AiModelPricing Create(AiProviderType provider, string model,
        decimal inputUsdPer1k, decimal outputUsdPer1k, DateTimeOffset effectiveFrom,
        Guid? createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model required.", nameof(model));
        if (inputUsdPer1k < 0 || outputUsdPer1k < 0) throw new ArgumentOutOfRangeException();
        return new(Guid.NewGuid(), provider, model, inputUsdPer1k, outputUsdPer1k, true, effectiveFrom, createdByUserId);
    }

    public void Deactivate() => IsActive = false;
}
