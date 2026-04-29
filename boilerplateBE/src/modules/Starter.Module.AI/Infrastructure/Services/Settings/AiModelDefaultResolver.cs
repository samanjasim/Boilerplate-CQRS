using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiModelDefaultResolver(
    AiDbContext db,
    IAiEntitlementResolver entitlements,
    IAiProviderFactory providerFactory) : IAiModelDefaultResolver
{
    private const double DefaultTemperature = 0.7;
    private const int DefaultMaxTokens = 4096;

    public async Task<Result<ResolvedModelDefault>> ResolveAsync(
        Guid? tenantId,
        AiAgentClass agentClass,
        AiProviderType? explicitProvider,
        string? explicitModel,
        double? explicitTemperature,
        int? explicitMaxTokens,
        CancellationToken ct = default)
    {
        var resolvedEntitlements = await entitlements.ResolveAsync(tenantId, ct);

        if (explicitProvider is { } provider && !string.IsNullOrWhiteSpace(explicitModel))
            return ResolveAllowed(
                provider,
                explicitModel,
                explicitTemperature,
                explicitMaxTokens,
                resolvedEntitlements);

        if (tenantId is Guid id)
        {
            var tenantDefault = await db.AiModelDefaults
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.TenantId == id && d.AgentClass == agentClass, ct);

            if (tenantDefault is not null)
                return ResolveAllowed(
                    tenantDefault.Provider,
                    tenantDefault.Model,
                    tenantDefault.Temperature,
                    tenantDefault.MaxTokens,
                    resolvedEntitlements);
        }

        var (platformProvider, platformModel) = ResolvePlatformDefault(agentClass);
        return ResolveAllowed(
            platformProvider,
            platformModel,
            explicitTemperature,
            explicitMaxTokens,
            resolvedEntitlements);
    }

    private Result<ResolvedModelDefault> ResolveAllowed(
        AiProviderType provider,
        string model,
        double? temperature,
        int? maxTokens,
        AiEntitlementsDto entitlements)
    {
        var normalizedModel = NormalizeModel(provider, model);

        if (!IsProviderAllowed(entitlements.AllowedProviders, provider))
            return Result.Failure<ResolvedModelDefault>(AiSettingsErrors.ProviderNotAllowed(provider.ToString()));

        if (!IsModelAllowed(entitlements.AllowedModels, provider, normalizedModel))
            return Result.Failure<ResolvedModelDefault>(AiSettingsErrors.ModelNotAllowed(normalizedModel));

        return Result.Success(new ResolvedModelDefault(
            provider,
            normalizedModel,
            temperature ?? DefaultTemperature,
            maxTokens ?? DefaultMaxTokens));
    }

    private (AiProviderType Provider, string Model) ResolvePlatformDefault(AiAgentClass agentClass)
    {
        if (agentClass == AiAgentClass.Embedding)
        {
            var provider = providerFactory.GetEmbeddingProviderType();
            return (provider, NormalizeModel(provider, providerFactory.GetEmbeddingModelId()));
        }

        var defaultProvider = providerFactory.GetDefaultProviderType();
        return (defaultProvider, NormalizeModel(defaultProvider, providerFactory.GetDefaultChatModelId()));
    }

    private static string NormalizeModel(AiProviderType provider, string model)
    {
        var trimmed = model.Trim();
        var prefix = $"{provider}:";
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..]
            : trimmed;
    }

    private static bool IsProviderAllowed(IReadOnlyList<string> allowedProviders, AiProviderType provider) =>
        allowedProviders.Count == 0 ||
        allowedProviders.Any(p => string.Equals(p, provider.ToString(), StringComparison.OrdinalIgnoreCase));

    private static bool IsModelAllowed(IReadOnlyList<string> allowedModels, AiProviderType provider, string model) =>
        allowedModels.Count == 0 ||
        allowedModels.Any(m =>
            string.Equals(m, model, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m, $"{provider}:{model}", StringComparison.OrdinalIgnoreCase));
}
