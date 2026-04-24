using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiToolMappers
{
    private const string UnknownModule = "Unknown";

    public static AiToolDto ToDto(this IAiToolDefinition definition, AiTool? dbRow) =>
        new(
            definition.Name,
            definition.Description,
            definition.Category,
            ModuleOf(definition),
            definition.RequiredPermission,
            definition.IsReadOnly,
            IsEnabled: dbRow?.IsEnabled ?? true,
            definition.ParameterSchema);

    public static AiToolDto ToDto(this AiTool row, IAiToolDefinition definition) =>
        new(
            row.Name,
            row.Description,
            row.Category,
            ModuleOf(definition),
            row.RequiredPermission,
            row.IsReadOnly,
            row.IsEnabled,
            definition.ParameterSchema);

    private static string ModuleOf(IAiToolDefinition definition)
    {
        if (definition is not IAiToolDefinitionModuleSource src)
            return UnknownModule;

        // Starter.Application → "Application" via DeriveModuleSource; surface as "Core" in
        // the catalog because that matches how the module is referred to in docs and UI.
        return src.ModuleSource == "Application" ? "Core" : src.ModuleSource;
    }
}
