using MediatR;
using Starter.Domain.Identity.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationInvitationEventHandler(
    ITriggerRuleEvaluator triggerRuleEvaluator)
    : INotificationHandler<InvitationAcceptedEvent>
{
    public async Task Handle(InvitationAcceptedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TenantId is null) return;

        await triggerRuleEvaluator.EvaluateAsync("invitation.accepted", notification.TenantId.Value, notification.UserId,
            new Dictionary<string, object>
            {
                ["userId"] = notification.UserId.ToString(),
                ["email"] = notification.Email,
                ["roleId"] = notification.RoleId.ToString()
            }, cancellationToken);
    }
}
