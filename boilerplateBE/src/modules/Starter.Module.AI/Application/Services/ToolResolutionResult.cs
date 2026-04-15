using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services;

internal sealed record ToolResolutionResult(
    IReadOnlyList<AiToolDefinitionDto> ProviderTools,
    IReadOnlyDictionary<string, IAiToolDefinition> DefinitionsByName);
