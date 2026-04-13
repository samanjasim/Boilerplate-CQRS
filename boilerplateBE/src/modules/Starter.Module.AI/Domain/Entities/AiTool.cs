using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiTool : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string ParameterSchema { get; private set; } = "{}";
    public string CommandType { get; private set; } = default!;
    public string RequiredPermission { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public bool IsEnabled { get; private set; }
    public bool IsReadOnly { get; private set; }

    private AiTool() { }

    private AiTool(
        Guid id,
        string name,
        string description,
        string parameterSchema,
        string commandType,
        string requiredPermission,
        string category,
        bool isEnabled,
        bool isReadOnly) : base(id)
    {
        Name = name;
        Description = description;
        ParameterSchema = parameterSchema;
        CommandType = commandType;
        RequiredPermission = requiredPermission;
        Category = category;
        IsEnabled = isEnabled;
        IsReadOnly = isReadOnly;
    }

    public static AiTool Create(
        string name,
        string description,
        string commandType,
        string requiredPermission,
        string category,
        string parameterSchema = "{}",
        bool isEnabled = true,
        bool isReadOnly = false)
    {
        return new AiTool(
            Guid.NewGuid(),
            name.Trim(),
            description.Trim(),
            parameterSchema,
            commandType.Trim(),
            requiredPermission.Trim(),
            category.Trim(),
            isEnabled,
            isReadOnly);
    }

    public void Toggle(bool enabled)
    {
        IsEnabled = enabled;
        ModifiedAt = DateTime.UtcNow;
    }
}
