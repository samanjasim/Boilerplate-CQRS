using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookFileEventHandler(
    IWebhookPublisher webhookPublisher)
    : INotificationHandler<FileUploadedEvent>, INotificationHandler<FileDeletedEvent>
{
    public async Task Handle(FileUploadedEvent notification, CancellationToken cancellationToken)
    {
        await webhookPublisher.PublishAsync("file.uploaded", notification.TenantId, new
        {
            fileId = notification.FileId,
            fileName = notification.FileName,
            size = notification.Size,
            contentType = notification.ContentType
        }, cancellationToken);
    }

    public async Task Handle(FileDeletedEvent notification, CancellationToken cancellationToken)
    {
        await webhookPublisher.PublishAsync("file.deleted", notification.TenantId, new
        {
            fileId = notification.FileId,
            fileName = notification.FileName
        }, cancellationToken);
    }
}
