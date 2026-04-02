using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookUserEventHandler(
    IApplicationDbContext context,
    IMessagePublisher messagePublisher,
    ILogger<WebhookUserEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>, INotificationHandler<UserUpdatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Load user to get TenantId (not in the event)
        var user = await context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

        if (user?.TenantId is null)
            return;

        await PublishWebhookAsync("user.created", user.TenantId, new
        {
            userId = notification.UserId,
            email = notification.Email,
            fullName = notification.FullName
        }, cancellationToken);
    }

    public async Task Handle(UserUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

        if (user?.TenantId is null)
            return;

        await PublishWebhookAsync("user.updated", user.TenantId, new
        {
            userId = notification.UserId,
            email = user.Email.Value
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
