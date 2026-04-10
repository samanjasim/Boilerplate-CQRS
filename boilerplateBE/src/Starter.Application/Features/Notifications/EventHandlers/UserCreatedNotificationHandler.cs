using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Notifications.EventHandlers;

internal sealed class UserCreatedNotificationHandler(
    IApplicationDbContext context,
    INotificationService notificationService,
    ILogger<UserCreatedNotificationHandler> logger) : INotificationHandler<UserCreatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await context.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

            if (user?.TenantId is not null)
            {
                await notificationService.CreateForTenantAdminsAsync(
                    user.TenantId.Value,
                    NotificationType.UserCreated,
                    "New user joined",
                    $"New user {notification.FullName} joined the organization.",
                    System.Text.Json.JsonSerializer.Serialize(new { userId = notification.UserId }),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification for UserCreatedEvent {UserId}", notification.UserId);
        }
    }
}
