using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Domain.Billing.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookBillingEventHandler(
    IApplicationDbContext context,
    IMessagePublisher messagePublisher,
    ILogger<WebhookBillingEventHandler> logger)
    : INotificationHandler<SubscriptionChangedEvent>
{
    public async Task Handle(SubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        await PublishWebhookAsync("subscription.changed", notification.TenantId, new
        {
            tenantId = notification.TenantId,
            oldPlanId = notification.OldPlanId,
            newPlanId = notification.NewPlanId
        }, cancellationToken);
    }

    private async Task PublishWebhookAsync(string eventType, Guid tenantId, object data, CancellationToken ct)
    {
        try
        {
            var hasSubscribers = await context.WebhookEndpoints
                .IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.IsActive, ct);

            if (!hasSubscribers)
                return;

            var payload = JsonSerializer.Serialize(new
            {
                id = $"evt_{Guid.NewGuid():N}",
                type = eventType,
                tenantId,
                timestamp = DateTime.UtcNow,
                data
            });

            await messagePublisher.PublishAsync(
                new DeliverWebhookMessage(tenantId, eventType, payload, DateTime.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish webhook event {EventType} for tenant {TenantId}",
                eventType, tenantId);
        }
    }
}
