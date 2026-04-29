using Starter.Abstractions.Ai;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiProviderCredentialResolver
{
    Task<Result<ResolvedProviderCredential>> ResolveAsync(
        Guid? tenantId,
        AiProviderType provider,
        CancellationToken ct = default);
}
