using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Webhooks.EventHandlers;

internal sealed class WebhookUserEventHandler(
    IApplicationDbContext context,
    IWebhookPublisher webhookPublisher)
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

        await webhookPublisher.PublishAsync("user.created", user.TenantId, new
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

        await webhookPublisher.PublishAsync("user.updated", user.TenantId, new
        {
            userId = notification.UserId,
            email = user.Email.Value
        }, cancellationToken);
    }
}
