using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

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
