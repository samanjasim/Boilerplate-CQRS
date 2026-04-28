using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Domain.Tenants.Events;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Persistence.Seed;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationTenantEventHandler(
    ITriggerRuleEvaluator triggerRuleEvaluator,
    CommunicationDbContext dbContext,
    ILogger<CommunicationTenantEventHandler> logger)
    : INotificationHandler<TenantCreatedEvent>
{
    public async Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Communication handling TenantCreatedEvent for {TenantId}", notification.TenantId);

        // Plan 5d-2 Task F2: seed AI approval required-notification rows for the new tenant
        // (idempotent — RequiredNotificationSeed skips already-present rows). Failure here
        // must not cascade — log and continue to trigger rule evaluation.
        try
        {
            var inserted = await RequiredNotificationSeed.SeedAiApprovalNotificationsAsync(
                dbContext, notification.TenantId, cancellationToken);

            if (inserted > 0)
            {
                logger.LogInformation(
                    "Seeded {Count} AI approval required-notification rows for new tenant {TenantId}",
                    inserted, notification.TenantId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to seed AI approval required-notification rows for tenant {TenantId}",
                notification.TenantId);
        }

        await triggerRuleEvaluator.EvaluateAsync("tenant.registered", notification.TenantId, null,
            new Dictionary<string, object>
            {
                ["tenantId"] = notification.TenantId.ToString()
            }, cancellationToken);
    }
}
