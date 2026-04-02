using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookInvitationEventHandler(
    IApplicationDbContext context,
    IMessagePublisher messagePublisher,
    ILogger<WebhookInvitationEventHandler> logger)
    : INotificationHandler<InvitationAcceptedEvent>
{
    public async Task Handle(InvitationAcceptedEvent notification, CancellationToken cancellationToken)
    {
        await PublishWebhookAsync("invitation.accepted", notification.TenantId, new
        {
            userId = notification.UserId,
            email = notification.Email,
            roleId = notification.RoleId
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
