using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookBillingEventHandler(
    IWebhookPublisher webhookPublisher)
    : INotificationHandler<SubscriptionChangedEvent>
{
    public async Task Handle(SubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        await webhookPublisher.PublishAsync("subscription.changed", (Guid?)notification.TenantId, new
        {
            tenantId = notification.TenantId,
            oldPlanId = notification.OldPlanId,
            newPlanId = notification.NewPlanId
        }, cancellationToken);
    }
}
