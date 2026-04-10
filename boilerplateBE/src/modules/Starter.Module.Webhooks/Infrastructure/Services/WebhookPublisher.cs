using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Webhooks.Application.Messages;
using Starter.Module.Webhooks.Infrastructure.Persistence;

namespace Starter.Module.Webhooks.Infrastructure.Services;

internal sealed class WebhookPublisher(
    WebhooksDbContext context,
    IMessagePublisher messagePublisher,
    ILogger<WebhookPublisher> logger) : IWebhookPublisher
{
    public async Task PublishAsync(string eventType, Guid? tenantId, object data, CancellationToken cancellationToken = default)
    {
        if (tenantId is null)
            return;

        try
        {
            var hasSubscribers = await context.WebhookEndpoints
                .IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.IsActive, cancellationToken);

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
                new DeliverWebhookMessage(tenantId.Value, eventType, payload, DateTime.UtcNow), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish webhook event {EventType} for tenant {TenantId}",
                eventType, tenantId);
        }
    }
}
