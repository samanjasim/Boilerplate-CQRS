using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Domain.Identity.Events;

namespace Starter.Module.Webhooks.Application.EventHandlers;

internal sealed class WebhookInvitationEventHandler(
    IWebhookPublisher webhookPublisher)
    : INotificationHandler<InvitationAcceptedEvent>
{
    public async Task Handle(InvitationAcceptedEvent notification, CancellationToken cancellationToken)
    {
        await webhookPublisher.PublishAsync("invitation.accepted", notification.TenantId, new
        {
            userId = notification.UserId,
            email = notification.Email,
            roleId = notification.RoleId
        }, cancellationToken);
    }
}
