using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Events;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Enums;
using System.Text.Json;

namespace Starter.Application.Features.Billing.EventHandlers;

internal sealed class SyncPlanFeaturesHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService,
    ILogger<SyncPlanFeaturesHandler> logger) : INotificationHandler<SubscriptionChangedEvent>
{
    public async Task Handle(SubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == notification.NewPlanId, cancellationToken);

            if (plan is null)
            {
                logger.LogWarning("SyncPlanFeaturesHandler: Plan {PlanId} not found for tenant {TenantId}",
                    notification.NewPlanId, notification.TenantId);
                return;
            }

            // 1. Load plan features JSON
            var planFeatures = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(plan.Features))
            {
                planFeatures = JsonSerializer.Deserialize<Dictionary<string, string>>(plan.Features)
                    ?? new Dictionary<string, string>();
            }

            // 2. Load all feature flags to build key → id dictionary
            var allFlags = await context.FeatureFlags
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var flagIdByKey = allFlags.ToDictionary(f => f.Key, f => f.Id);

            // 3. Load existing TenantFeatureFlags for this tenant
            var existingOverrides = await context.TenantFeatureFlags
                .IgnoreQueryFilters()
                .Where(tf => tf.TenantId == notification.TenantId)
                .ToListAsync(cancellationToken);

            var existingByFlagId = existingOverrides.ToDictionary(tf => tf.FeatureFlagId);

            // 4. Apply plan features
            var planFlagIds = new HashSet<Guid>();

            foreach (var (key, value) in planFeatures)
            {
                if (!flagIdByKey.TryGetValue(key, out var flagId))
                {
                    logger.LogDebug("SyncPlanFeaturesHandler: Feature flag key '{Key}' from plan not found in FeatureFlags table", key);
                    continue;
                }

                planFlagIds.Add(flagId);

                if (existingByFlagId.TryGetValue(flagId, out var existing))
                {
                    if (existing.Source == OverrideSource.PlanSubscription)
                    {
                        existing.UpdateValue(value, OverrideSource.PlanSubscription);
                    }
                    // If Source == Manual, skip to preserve tenant opt-out
                }
                else
                {
                    var newOverride = TenantFeatureFlag.Create(
                        notification.TenantId,
                        flagId,
                        value,
                        OverrideSource.PlanSubscription);
                    await context.TenantFeatureFlags.AddAsync(newOverride, cancellationToken);
                }
            }

            // 5. Remove PlanSubscription overrides for flags NOT in the new plan
            var toRemove = existingOverrides
                .Where(tf => tf.Source == OverrideSource.PlanSubscription && !planFlagIds.Contains(tf.FeatureFlagId))
                .ToList();

            if (toRemove.Count > 0)
                context.TenantFeatureFlags.RemoveRange(toRemove);

            // 6. Save changes
            await context.SaveChangesAsync(cancellationToken);

            // 7. Invalidate feature flag cache for this tenant
            await featureFlagService.InvalidateCacheAsync(notification.TenantId, cancellationToken);

            logger.LogInformation(
                "SyncPlanFeaturesHandler: Synced {Count} plan features for tenant {TenantId} (plan {PlanId}), removed {RemovedCount} stale overrides",
                planFlagIds.Count, notification.TenantId, notification.NewPlanId, toRemove.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SyncPlanFeaturesHandler: Failed to sync plan features for tenant {TenantId}, plan {PlanId}",
                notification.TenantId, notification.NewPlanId);
        }
    }
}
