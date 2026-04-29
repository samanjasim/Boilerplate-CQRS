using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiModelDefaultResolver
{
    Task<Result<ResolvedModelDefault>> ResolveAsync(
        Guid? tenantId,
        AiAgentClass agentClass,
        AiProviderType? explicitProvider,
        string? explicitModel,
        double? explicitTemperature,
        int? explicitMaxTokens,
        CancellationToken ct = default);
}
