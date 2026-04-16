using MediatR;
using Starter.Domain.Common.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationFileEventHandler(
    ITriggerRuleEvaluator triggerRuleEvaluator)
    : INotificationHandler<FileUploadedEvent>,
      INotificationHandler<FileDeletedEvent>
{
    public async Task Handle(FileUploadedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TenantId is null) return;

        await triggerRuleEvaluator.EvaluateAsync("file.uploaded", notification.TenantId.Value, null,
            new Dictionary<string, object>
            {
                ["fileId"] = notification.FileId.ToString(),
                ["fileName"] = notification.FileName,
                ["size"] = notification.Size,
                ["contentType"] = notification.ContentType
            }, cancellationToken);
    }

    public async Task Handle(FileDeletedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TenantId is null) return;

        await triggerRuleEvaluator.EvaluateAsync("file.deleted", notification.TenantId.Value, null,
            new Dictionary<string, object>
            {
                ["fileId"] = notification.FileId.ToString(),
                ["fileName"] = notification.FileName
            }, cancellationToken);
    }
}
