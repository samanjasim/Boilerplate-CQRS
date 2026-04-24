using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Adapter that makes an <c>[AiTool]</c>-decorated MediatR request type look like an
/// <see cref="IAiToolDefinition"/>. Constructed by <see cref="AiToolDiscoveryExtensions"/>
/// during assembly scan. Also exposes <see cref="IAiToolDefinitionModuleSource"/> so the
/// tool catalog can group by module.
/// </summary>
internal sealed class AttributedAiToolDefinition : IAiToolDefinition, IAiToolDefinitionModuleSource
{
    public AttributedAiToolDefinition(
        Type commandType,
        AiToolAttribute attribute,
        JsonElement parameterSchema,
        string moduleSource)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleSource);

        CommandType = commandType;
        Name = attribute.Name;
        Description = attribute.Description;
        Category = attribute.Category;
        RequiredPermission = attribute.RequiredPermission;
        IsReadOnly = attribute.IsReadOnly;
        ParameterSchema = parameterSchema;
        ModuleSource = moduleSource;
    }

    public string Name { get; }
    public string Description { get; }
    public JsonElement ParameterSchema { get; }
    public Type CommandType { get; }
    public string RequiredPermission { get; }
    public string Category { get; }
    public bool IsReadOnly { get; }
    public string ModuleSource { get; }
}
