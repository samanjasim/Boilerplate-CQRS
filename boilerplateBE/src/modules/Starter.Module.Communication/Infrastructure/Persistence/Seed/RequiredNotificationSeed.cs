using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds <see cref="RequiredNotification"/> rows that mark certain event categories as
/// "always-on" for a tenant — users cannot opt out of these via their notification
/// preferences.
///
/// Plan 5d-2 (Task F2) introduces this for the four AI agent approval lifecycle events:
/// these are governance-critical signals (a sensitive tool is awaiting human review,
/// or has been approved/denied/expired) and tenants must always receive at least the
/// in-app notification regardless of per-user opt-out.
///
/// Idempotent on the (TenantId, Category) pair — already-seeded rows are skipped, so
/// the seeder is safe to run on every startup. Existing tenants are seeded by the
/// startup loop; brand-new tenants pick up the rows via
/// <c>CommunicationTenantEventHandler</c> on <see cref="Starter.Domain.Tenants.Events.TenantCreatedEvent"/>.
/// </summary>
public static class RequiredNotificationSeed
{
    /// <summary>
    /// Event-key categories the AI module raises around the agent-approval lifecycle.
    /// Mirrors the event names emitted by <c>CommunicationAiEventHandler</c> (Task F1)
    /// and the webhook event types fired by <c>PublishWebhookOnAgentApproval</c> (Task F3).
    /// </summary>
    public static readonly string[] AiApprovalEventKeys =
    [
        "ai.agent.approval.pending",
        "ai.agent.approval.approved",
        "ai.agent.approval.denied",
        "ai.agent.approval.expired",
    ];

    /// <summary>
    /// Seeds the four AI approval categories for every existing tenant (idempotent).
    /// Called from <c>CommunicationModule.SeedDataAsync</c> on startup.
    /// </summary>
    public static async Task SeedAllTenantsAsync(
        CommunicationDbContext db,
        IApplicationDbContext appDb,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await appDb.Tenants
            .IgnoreQueryFilters()
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count == 0)
        {
            logger.LogDebug("No tenants present; skipping required-notification seed.");
            return;
        }

        var inserted = 0;
        foreach (var tenantId in tenantIds)
        {
            inserted += await SeedAiApprovalNotificationsAsync(db, tenantId, cancellationToken);
        }

        if (inserted > 0)
        {
            logger.LogInformation(
                "Seeded {Count} AI approval required-notification rows across {TenantCount} tenant(s)",
                inserted, tenantIds.Count);
        }
        else
        {
            logger.LogDebug("All AI approval required-notification rows already present.");
        }
    }

    /// <summary>
    /// Seeds the four AI approval categories for a single tenant. Returns the number of
    /// rows inserted (0 if all four were already present). Caller owns the transaction
    /// boundary — this method calls <c>SaveChangesAsync</c> exactly once.
    /// </summary>
    public static async Task<int> SeedAiApprovalNotificationsAsync(
        CommunicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existingCategories = await db.RequiredNotifications
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && AiApprovalEventKeys.Contains(r.Category))
            .Select(r => r.Category)
            .ToListAsync(cancellationToken);

        var existing = existingCategories.ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var key in AiApprovalEventKeys)
        {
            if (existing.Contains(key)) continue;

            db.RequiredNotifications.Add(
                RequiredNotification.Create(tenantId, key, NotificationChannel.InApp));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }
}
