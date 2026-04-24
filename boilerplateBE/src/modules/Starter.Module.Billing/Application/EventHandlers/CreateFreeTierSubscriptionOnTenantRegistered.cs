using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;
using Starter.Module.Billing.Domain.Entities;
using Starter.Module.Billing.Infrastructure.Persistence;

namespace Starter.Module.Billing.Application.EventHandlers;

/// <summary>
/// Provisions a free-tier subscription for newly registered tenants.
///
/// Subscribes to <see cref="TenantRegisteredEvent"/> via the MassTransit
/// transactional outbox: by the time this consumer runs, the tenant row is
/// already committed in <c>ApplicationDbContext</c>, so this handler can
/// safely query for the active free plan and write the subscription.
///
/// If no free plan is seeded (or none is active), the handler logs and does
/// nothing — this is intentional, the tenant simply has no subscription until
/// an admin assigns one.
///
/// Idempotency: this handler performs a manual "already exists" check against
/// <c>BillingDbContext.TenantSubscriptions</c>. The MassTransit <c>InboxState</c>
/// dedup table lives in <c>ApplicationDbContext</c>, but this consumer now writes
/// through <c>BillingDbContext</c>, so cross-context outbox plumbing would be
/// required to share dedup. The manual check is simpler and equally correct
/// because <c>TenantId</c> is unique per subscription.
/// </summary>
public sealed class CreateFreeTierSubscriptionOnTenantRegistered(
    BillingDbContext context,
    IUsageTracker usageTracker,
    IWebhookPublisher webhookPublisher,
    ILogger<CreateFreeTierSubscriptionOnTenantRegistered> logger)
    : IConsumer<TenantRegisteredEvent>
{
    public async Task Consume(ConsumeContext<TenantRegisteredEvent> context_)
    {
        var ct = context_.CancellationToken;
        var evt = context_.Message;

        await CreateFreeTierSubscription(evt, ct);
    }

    private async Task CreateFreeTierSubscription(TenantRegisteredEvent evt, CancellationToken ct)
    {
        // Manual idempotency check — replaces InboxState dedup, which would require
        // cross-DbContext outbox plumbing now that this handler writes via BillingDbContext.
        if (await context.TenantSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.TenantId == evt.TenantId, ct))
        {
            logger.LogInformation(
                "Tenant {TenantId} already has subscription, skipping",
                evt.TenantId);
            return;
        }

        var freePlan = await context.SubscriptionPlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.IsFree && p.IsActive, ct);

        if (freePlan is null)
        {
            logger.LogInformation(
                "No active free plan seeded — tenant {TenantId} registered without a subscription",
                evt.TenantId);
            return;
        }

        var now = DateTime.UtcNow;
        var subscription = TenantSubscription.Create(
            evt.TenantId,
            freePlan.Id,
            lockedMonthlyPrice: 0,
            lockedAnnualPrice: 0,
            freePlan.Currency,
            BillingInterval.Monthly,
            currentPeriodStart: now,
            currentPeriodEnd: now.AddYears(100),
            trialEndAt: null,
            autoRenew: false);

        context.TenantSubscriptions.Add(subscription);
        await context.SaveChangesAsync(ct);

        // Seed initial seat-count usage so quota checks have a baseline
        await usageTracker.SetAsync(evt.TenantId, "users", 1, ct);

        // Cross-module side effect via capability. If Webhooks is installed,
        // dispatch a "subscription.created" event. NullWebhookPublisher no-ops
        // otherwise.
        await webhookPublisher.PublishAsync(
            eventType: "subscription.created",
            tenantId: evt.TenantId,
            data: new
            {
                tenantId = evt.TenantId,
                subscriptionId = subscription.Id,
                planId = freePlan.Id,
                isFree = true
            },
            cancellationToken: ct);

        logger.LogInformation(
            "Created free-tier subscription for tenant {TenantId} (plan {PlanId})",
            evt.TenantId, freePlan.Id);
    }
}
