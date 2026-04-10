using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Domain.Identity.Events;

namespace Starter.Module.Webhooks.Application.EventHandlers;

internal sealed class WebhookRoleEventHandler(
    IWebhookPublisher webhookPublisher)
    : INotificationHandler<RoleCreatedEvent>, INotificationHandler<RoleUpdatedEvent>
{
    public async Task Handle(RoleCreatedEvent notification, CancellationToken cancellationToken)
    {
        await webhookPublisher.PublishAsync("role.created", notification.TenantId, new
        {
            roleId = notification.RoleId,
            name = notification.Name
        }, cancellationToken);
    }

    public async Task Handle(RoleUpdatedEvent notification, CancellationToken cancellationToken)
    {
        await webhookPublisher.PublishAsync("role.updated", notification.TenantId, new
        {
            roleId = notification.RoleId,
            name = notification.Name
        }, cancellationToken);
    }
}
