using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;

namespace Starter.Application.Features.Notifications.EventHandlers;

internal sealed class PasswordChangedNotificationHandler(
    IApplicationDbContext context,
    INotificationService notificationService,
    ILogger<PasswordChangedNotificationHandler> logger) : INotificationHandler<PasswordChangedEvent>
{
    public async Task Handle(PasswordChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var user = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken);

            if (user is not null)
            {
                await notificationService.CreateAsync(
                    user.Id,
                    user.TenantId,
                    NotificationType.PasswordChanged,
                    "Password changed",
                    "Your password was changed successfully. If you did not make this change, please contact support immediately.",
                    ct: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification for PasswordChangedEvent {UserId}", notification.UserId);
        }
    }
}
