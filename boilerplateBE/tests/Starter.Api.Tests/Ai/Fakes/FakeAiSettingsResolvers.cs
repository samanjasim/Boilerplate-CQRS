using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Api.Tests.Ai.Fakes;

internal sealed class FakeAiModelDefaultResolver(
    AiProviderType provider = AiProviderType.OpenAI,
    string model = "gpt-4o-mini") : IAiModelDefaultResolver
{
    public Task<Result<ResolvedModelDefault>> ResolveAsync(
        Guid? tenantId,
        AiAgentClass agentClass,
        AiProviderType? explicitProvider,
        string? explicitModel,
        double? explicitTemperature,
        int? explicitMaxTokens,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success(new ResolvedModelDefault(
            explicitProvider ?? provider,
            explicitModel ?? model,
            explicitTemperature ?? 0.7,
            explicitMaxTokens ?? 4096)));
    }
}

internal sealed class FakeAiProviderCredentialResolver(
    string secret = "test-provider-key") : IAiProviderCredentialResolver
{
    public Task<Result<ResolvedProviderCredential>> ResolveAsync(
        Guid? tenantId,
        AiProviderType requestedProvider,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success(new ResolvedProviderCredential(
            requestedProvider,
            secret,
            ProviderCredentialSource.Platform,
            ProviderCredentialId: null)));
    }
}
