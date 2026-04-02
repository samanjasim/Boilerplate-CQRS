using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookRoleEventHandler(
    IApplicationDbContext context,
    IMessagePublisher messagePublisher,
    ILogger<WebhookRoleEventHandler> logger)
    : INotificationHandler<RoleCreatedEvent>, INotificationHandler<RoleUpdatedEvent>
{
    public async Task Handle(RoleCreatedEvent notification, CancellationToken cancellationToken)
    {
        await PublishWebhookAsync("role.created", notification.TenantId, new
        {
            roleId = notification.RoleId,
            name = notification.Name
        }, cancellationToken);
    }

    public async Task Handle(RoleUpdatedEvent notification, CancellationToken cancellationToken)
    {
        await PublishWebhookAsync("role.updated", notification.TenantId, new
        {
            roleId = notification.RoleId,
            name = notification.Name
        }, cancellationToken);
    }

    private async Task PublishWebhookAsync(string eventType, Guid? tenantId, object data, CancellationToken ct)
    {
        if (tenantId is null)
            return;

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
                new DeliverWebhookMessage(tenantId.Value, eventType, payload, DateTime.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish webhook event {EventType} for tenant {TenantId}",
                eventType, tenantId);
        }
    }
}
