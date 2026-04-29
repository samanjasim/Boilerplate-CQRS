using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiEntitlementResolver(IFeatureFlagService featureFlags) : IAiEntitlementResolver
{
    public async Task<AiEntitlementsDto> ResolveAsync(CancellationToken ct = default)
    {
        var allowedProviders = await featureFlags.GetValueAsync<string[]>("ai.providers.allowed", ct);
        var allowedModels = await featureFlags.GetValueAsync<string[]>("ai.models.allowed", ct);

        return new AiEntitlementsDto(
            TotalMonthlyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", ct),
            TotalDailyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", ct),
            PlatformMonthlyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.platform_monthly_usd", ct),
            PlatformDailyUsd: await featureFlags.GetValueAsync<decimal>("ai.cost.platform_daily_usd", ct),
            RequestsPerMinute: await featureFlags.GetValueAsync<int>("ai.agents.requests_per_minute_default", ct),
            ByokEnabled: await featureFlags.GetValueAsync<bool>("ai.provider_keys.byok_enabled", ct),
            WidgetsEnabled: await featureFlags.GetValueAsync<bool>("ai.widgets.enabled", ct),
            WidgetMaxCount: await featureFlags.GetValueAsync<int>("ai.widgets.max_count", ct),
            WidgetMonthlyTokens: await featureFlags.GetValueAsync<int>("ai.widgets.monthly_tokens", ct),
            WidgetDailyTokens: await featureFlags.GetValueAsync<int>("ai.widgets.daily_tokens", ct),
            WidgetRequestsPerMinute: await featureFlags.GetValueAsync<int>("ai.widgets.requests_per_minute", ct),
            AllowedProviders: allowedProviders,
            AllowedModels: allowedModels);
    }
}
