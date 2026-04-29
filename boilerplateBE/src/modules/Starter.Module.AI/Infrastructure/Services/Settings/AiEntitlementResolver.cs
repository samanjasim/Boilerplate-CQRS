using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiEntitlementResolver(IFeatureFlagService featureFlags) : IAiEntitlementResolver
{
    public async Task<AiEntitlementsDto> ResolveAsync(CancellationToken ct = default)
        => await ResolveAsync(tenantId: null, ct);

    public async Task<AiEntitlementsDto> ResolveAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var allowedProviders = await GetValueAsync<string[]>("ai.providers.allowed", tenantId, ct);
        var allowedModels = await GetValueAsync<string[]>("ai.models.allowed", tenantId, ct);

        return new AiEntitlementsDto(
            TotalMonthlyUsd: await GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", tenantId, ct),
            TotalDailyUsd: await GetValueAsync<decimal>("ai.cost.tenant_daily_usd", tenantId, ct),
            PlatformMonthlyUsd: await GetValueAsync<decimal>("ai.cost.platform_monthly_usd", tenantId, ct),
            PlatformDailyUsd: await GetValueAsync<decimal>("ai.cost.platform_daily_usd", tenantId, ct),
            RequestsPerMinute: await GetValueAsync<int>("ai.agents.requests_per_minute_default", tenantId, ct),
            ByokEnabled: await GetValueAsync<bool>("ai.provider_keys.byok_enabled", tenantId, ct),
            WidgetsEnabled: await GetValueAsync<bool>("ai.widgets.enabled", tenantId, ct),
            WidgetMaxCount: await GetValueAsync<int>("ai.widgets.max_count", tenantId, ct),
            WidgetMonthlyTokens: await GetValueAsync<int>("ai.widgets.monthly_tokens", tenantId, ct),
            WidgetDailyTokens: await GetValueAsync<int>("ai.widgets.daily_tokens", tenantId, ct),
            WidgetRequestsPerMinute: await GetValueAsync<int>("ai.widgets.requests_per_minute", tenantId, ct),
            AllowedProviders: allowedProviders,
            AllowedModels: allowedModels);
    }

    private async Task<T> GetValueAsync<T>(string key, Guid? tenantId, CancellationToken ct)
        => tenantId.HasValue
            ? await featureFlags.GetValueForTenantAsync<T>(key, tenantId, ct)
            : await featureFlags.GetValueAsync<T>(key, ct);
}
