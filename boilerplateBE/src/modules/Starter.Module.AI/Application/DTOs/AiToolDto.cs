using System.Text.Json;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiToolDto(
    string Name,
    string Description,
    string Category,
    string RequiredPermission,
    bool IsReadOnly,
    bool IsEnabled,
    JsonElement ParameterSchema);
