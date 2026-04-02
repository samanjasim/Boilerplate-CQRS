using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Domain.Common.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookFileEventHandler(
    IApplicationDbContext context,
    IMessagePublisher messagePublisher,
    ILogger<WebhookFileEventHandler> logger)
    : INotificationHandler<FileUploadedEvent>, INotificationHandler<FileDeletedEvent>
{
    public async Task Handle(FileUploadedEvent notification, CancellationToken cancellationToken)
    {
        await PublishWebhookAsync("file.uploaded", notification.TenantId, new
        {
            fileId = notification.FileId,
            fileName = notification.FileName,
            size = notification.Size,
            contentType = notification.ContentType
        }, cancellationToken);
    }

    public async Task Handle(FileDeletedEvent notification, CancellationToken cancellationToken)
    {
        await PublishWebhookAsync("file.deleted", notification.TenantId, new
        {
            fileId = notification.FileId,
            fileName = notification.FileName
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
