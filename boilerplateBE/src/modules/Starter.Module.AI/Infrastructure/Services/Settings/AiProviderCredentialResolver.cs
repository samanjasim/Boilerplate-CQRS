using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiProviderCredentialResolver(
    AiDbContext db,
    IAiTenantSettingsResolver tenantSettings,
    IAiEntitlementResolver entitlements,
    IConfiguration configuration,
    IAiSecretProtector secrets) : IAiProviderCredentialResolver
{
    public async Task<Result<ResolvedProviderCredential>> ResolveAsync(
        Guid? tenantId,
        AiProviderType provider,
        CancellationToken ct = default)
    {
        if (tenantId is null)
            return ResolvePlatform(provider);

        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        if (!resolvedEntitlements.ByokEnabled)
            return ResolvePlatform(provider);

        if (!IsProviderAllowed(resolvedEntitlements.AllowedProviders, provider))
            return Result.Failure<ResolvedProviderCredential>(AiSettingsErrors.ProviderNotAllowed(provider.ToString()));

        var settings = await tenantSettings.GetOrDefaultAsync(tenantId.Value, ct);
        return settings.RequestedProviderCredentialPolicy switch
        {
            ProviderCredentialPolicy.PlatformOnly => ResolvePlatform(provider),
            ProviderCredentialPolicy.TenantKeysAllowed => await ResolveTenantOrPlatformAsync(tenantId.Value, provider, ct),
            ProviderCredentialPolicy.TenantKeysRequired => await ResolveRequiredTenantAsync(tenantId.Value, provider, ct),
            _ => ResolvePlatform(provider)
        };
    }

    private async Task<Result<ResolvedProviderCredential>> ResolveTenantOrPlatformAsync(
        Guid tenantId,
        AiProviderType provider,
        CancellationToken ct)
    {
        var credential = await FindActiveTenantCredentialAsync(tenantId, provider, ct);
        return credential is null
            ? ResolvePlatform(provider)
            : ResolveTenant(provider, credential.Id, credential.EncryptedSecret);
    }

    private async Task<Result<ResolvedProviderCredential>> ResolveRequiredTenantAsync(
        Guid tenantId,
        AiProviderType provider,
        CancellationToken ct)
    {
        var credential = await FindActiveTenantCredentialAsync(tenantId, provider, ct);
        return credential is null
            ? Result.Failure<ResolvedProviderCredential>(AiSettingsErrors.TenantKeyRequired(provider.ToString()))
            : ResolveTenant(provider, credential.Id, credential.EncryptedSecret);
    }

    private async Task<TenantCredentialProjection?> FindActiveTenantCredentialAsync(
        Guid tenantId,
        AiProviderType provider,
        CancellationToken ct)
    {
        return await db.AiProviderCredentials
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId
                        && c.Provider == provider
                        && c.Status == ProviderCredentialStatus.Active)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TenantCredentialProjection(c.Id, c.EncryptedSecret))
            .FirstOrDefaultAsync(ct);
    }

    private Result<ResolvedProviderCredential> ResolveTenant(
        AiProviderType provider,
        Guid credentialId,
        string encryptedSecret)
    {
        var secret = secrets.Unprotect(encryptedSecret);
        return Result.Success(new ResolvedProviderCredential(
            provider,
            secret,
            ProviderCredentialSource.Tenant,
            credentialId));
    }

    private Result<ResolvedProviderCredential> ResolvePlatform(AiProviderType provider)
    {
        var key = provider switch
        {
            AiProviderType.OpenAI => configuration["AI:Providers:OpenAI:ApiKey"],
            AiProviderType.Anthropic => configuration["AI:Providers:Anthropic:ApiKey"],
            AiProviderType.Ollama => string.Empty,
            _ => null
        };

        if (provider != AiProviderType.Ollama && string.IsNullOrWhiteSpace(key))
            return Result.Failure<ResolvedProviderCredential>(AiSettingsErrors.PlatformCredentialMissing(provider.ToString()));

        return Result.Success(new ResolvedProviderCredential(
            provider,
            key ?? string.Empty,
            ProviderCredentialSource.Platform,
            ProviderCredentialId: null));
    }

    private static bool IsProviderAllowed(IReadOnlyList<string> allowedProviders, AiProviderType provider)
    {
        return allowedProviders.Count == 0
               || allowedProviders.Any(p => string.Equals(p, provider.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record TenantCredentialProjection(Guid Id, string EncryptedSecret);
}
