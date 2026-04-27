namespace Starter.Module.AI.Application.Services.Costs;

/// <summary>
/// Resolves the effective `(MonthlyUsd, DailyUsd, Rpm)` caps for a `(TenantId, AssistantId)` pair
/// by reading plan ceilings from `IFeatureFlagService` and applying per-agent overrides
/// from `AiAssistant`. Lowest non-null value wins per dimension.
///
/// Cached 60s via `ICacheService`; invalidate explicitly when an `AiAssistant` is updated
/// (`AssistantUpdatedEvent`) or when the tenant's subscription plan changes
/// (`SubscriptionChangedEvent`).
/// </summary>
public interface ICostCapResolver
{
    Task<EffectiveCaps> ResolveAsync(Guid tenantId, Guid assistantId, CancellationToken ct = default);

    Task InvalidateAsync(Guid tenantId, Guid assistantId, CancellationToken ct = default);

    Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default);
}
