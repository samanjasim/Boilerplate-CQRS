namespace Starter.Domain.Common;

public sealed class SystemSetting : BaseAuditableEntity
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public bool IsSecret { get; private set; }
    public string DataType { get; private set; } = "text";
    public Guid? TenantId { get; private set; }

    private SystemSetting() { }

    private SystemSetting(Guid id) : base(id) { }

    public static SystemSetting Create(
        string key,
        string value,
        Guid? tenantId = null,
        string? description = null,
        string? category = null,
        bool isSecret = false,
        string dataType = "text")
    {
        return new SystemSetting(Guid.NewGuid())
        {
            Key = key,
            Value = value,
            TenantId = tenantId,
            Description = description,
            Category = category,
            IsSecret = isSecret,
            DataType = dataType
        };
    }

    public void UpdateValue(string value)
    {
        Value = value;
    }

    public void UpdateDataType(string dataType)
    {
        DataType = dataType;
    }
}
