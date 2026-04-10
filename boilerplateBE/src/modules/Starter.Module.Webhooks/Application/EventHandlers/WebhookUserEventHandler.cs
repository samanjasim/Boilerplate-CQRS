using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Domain.Identity.Events;

namespace Starter.Module.Webhooks.Application.EventHandlers;

internal sealed class WebhookUserEventHandler(
    IUserReader userReader,
    IWebhookPublisher webhookPublisher)
    : INotificationHandler<UserCreatedEvent>, INotificationHandler<UserUpdatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Load user via reader to get TenantId (not in the event)
        var user = await userReader.GetAsync(notification.UserId, cancellationToken);

        if (user?.TenantId is null)
            return;

        await webhookPublisher.PublishAsync("user.created", user.TenantId, new
        {
            userId = notification.UserId,
            email = notification.Email,
            fullName = notification.FullName
        }, cancellationToken);
    }

    public async Task Handle(UserUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var user = await userReader.GetAsync(notification.UserId, cancellationToken);

        if (user?.TenantId is null)
            return;

        await webhookPublisher.PublishAsync("user.updated", user.TenantId, new
        {
            userId = notification.UserId,
            email = user.Email
        }, cancellationToken);
    }
}
