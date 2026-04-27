using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services.Costs;

internal sealed class CostCapResolver(
    AiDbContext db,
    IFeatureFlagService featureFlags,
    ICacheService cache) : ICostCapResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public Task<EffectiveCaps> ResolveAsync(Guid tenantId, Guid assistantId, CancellationToken ct = default)
    {
        var key = CacheKey(tenantId, assistantId);
        return cache.GetOrSetAsync(key, async () => await LoadAsync(tenantId, assistantId, ct), CacheTtl, ct);
    }

    public Task InvalidateAsync(Guid tenantId, Guid assistantId, CancellationToken ct = default) =>
        cache.RemoveAsync(CacheKey(tenantId, assistantId), ct);

    public Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        cache.RemoveByPrefixAsync($"ai:cap:{tenantId}:", ct);

    private async Task<EffectiveCaps> LoadAsync(Guid tenantId, Guid assistantId, CancellationToken ct)
    {
        // IFeatureFlagService resolves tenant from ICurrentUserService implicitly. For operational
        // agents (no HTTP context), the trigger handler must wrap the call site in an
        // ICurrentUserService scope that returns the trigger's tenant id.
        var planMonthly = await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", ct);
        var planDaily = await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", ct);
        var planRpm = await featureFlags.GetValueAsync<int>("ai.agents.requests_per_minute_default", ct);

        var assistant = await db.AiAssistants
            .AsNoTracking()
            .Where(a => a.Id == assistantId)
            .Select(a => new
            {
                a.MonthlyCostCapUsd,
                a.DailyCostCapUsd,
                a.RequestsPerMinute
            })
            .FirstOrDefaultAsync(ct);

        var monthly = MinNonNull(planMonthly, assistant?.MonthlyCostCapUsd);
        var daily = MinNonNull(planDaily, assistant?.DailyCostCapUsd);
        var rpm = MinNonNull(planRpm, assistant?.RequestsPerMinute);

        return new EffectiveCaps(monthly, daily, rpm);
    }

    private static decimal MinNonNull(decimal plan, decimal? agent) =>
        agent is null ? plan : Math.Min(plan, agent.Value);

    private static int MinNonNull(int plan, int? agent) =>
        agent is null ? plan : Math.Min(plan, agent.Value);

    private static string CacheKey(Guid tenantId, Guid assistantId) =>
        $"ai:cap:{tenantId}:{assistantId}";
}
