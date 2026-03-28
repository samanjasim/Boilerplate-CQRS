using Starter.Domain.Common;

namespace Starter.Domain.FeatureFlags.Entities;

public sealed class TenantFeatureFlag : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid FeatureFlagId { get; private set; }
    public string Value { get; private set; } = default!;

    public FeatureFlag FeatureFlag { get; private set; } = default!;

    private TenantFeatureFlag() { }

    private TenantFeatureFlag(Guid id, Guid tenantId, Guid featureFlagId, string value) : base(id)
    {
        TenantId = tenantId;
        FeatureFlagId = featureFlagId;
        Value = value;
    }

    public static TenantFeatureFlag Create(Guid tenantId, Guid featureFlagId, string value)
    {
        return new TenantFeatureFlag(Guid.NewGuid(), tenantId, featureFlagId, value);
    }

    public void UpdateValue(string value)
    {
        Value = value;
        ModifiedAt = DateTime.UtcNow;
    }
}
