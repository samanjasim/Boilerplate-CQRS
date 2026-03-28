using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Enums;

namespace Starter.Domain.FeatureFlags.Entities;

public sealed class FeatureFlag : AggregateRoot
{
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string DefaultValue { get; private set; } = default!;
    public FlagValueType ValueType { get; private set; }
    public FlagCategory Category { get; private set; }
    public bool IsSystem { get; private set; }

    private readonly List<TenantFeatureFlag> _tenantOverrides = [];
    public IReadOnlyCollection<TenantFeatureFlag> TenantOverrides => _tenantOverrides.AsReadOnly();

    private FeatureFlag() { }

    private FeatureFlag(
        Guid id, string key, string name, string? description,
        string defaultValue, FlagValueType valueType, FlagCategory category, bool isSystem) : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        DefaultValue = defaultValue;
        ValueType = valueType;
        Category = category;
        IsSystem = isSystem;
    }

    public static FeatureFlag Create(
        string key, string name, string? description,
        string defaultValue, FlagValueType valueType, FlagCategory category, bool isSystem)
    {
        return new FeatureFlag(Guid.NewGuid(), key.Trim().ToLowerInvariant(), name.Trim(),
            description?.Trim(), defaultValue, valueType, category, isSystem);
    }

    public void Update(string name, string? description, string defaultValue, FlagCategory category)
    {
        Name = name.Trim();
        Description = description?.Trim();
        DefaultValue = defaultValue;
        Category = category;
        ModifiedAt = DateTime.UtcNow;
    }
}
