using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Enums;

namespace Starter.Domain.FeatureFlags.Entities;

public sealed class TenantFeatureFlag : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid FeatureFlagId { get; private set; }
    public string Value { get; private set; } = default!;
    public OverrideSource Source { get; private set; } = OverrideSource.Manual;

    public FeatureFlag FeatureFlag { get; private set; } = default!;

    private TenantFeatureFlag() { }

    private TenantFeatureFlag(Guid id, Guid tenantId, Guid featureFlagId, string value) : base(id)
    {
        TenantId = tenantId;
        FeatureFlagId = featureFlagId;
        Value = value;
    }

    public static TenantFeatureFlag Create(Guid tenantId, Guid featureFlagId, string value, OverrideSource source = OverrideSource.Manual)
    {
        return new TenantFeatureFlag(Guid.NewGuid(), tenantId, featureFlagId, value) { Source = source };
    }

    public void UpdateValue(string value, OverrideSource source)
    {
        Value = value;
        Source = source;
        ModifiedAt = DateTime.UtcNow;
    }
}
