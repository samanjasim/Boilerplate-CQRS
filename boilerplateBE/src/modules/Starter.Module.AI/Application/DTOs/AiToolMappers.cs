using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiToolMappers
{
    public static AiToolDto ToDto(this IAiToolDefinition definition, AiTool? dbRow) =>
        new(
            definition.Name,
            definition.Description,
            definition.Category,
            definition.RequiredPermission,
            definition.IsReadOnly,
            // New tools default to enabled; admin can disable.
            IsEnabled: dbRow?.IsEnabled ?? true,
            definition.ParameterSchema);

    public static AiToolDto ToDto(this AiTool row, IAiToolDefinition definition) =>
        new(
            row.Name,
            row.Description,
            row.Category,
            row.RequiredPermission,
            row.IsReadOnly,
            row.IsEnabled,
            definition.ParameterSchema);
}
