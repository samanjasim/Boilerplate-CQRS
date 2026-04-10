using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Notifications.Commands.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var notification = await context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId.Value, cancellationToken);

        if (notification is null)
            return Result.Failure(Error.NotFound("Notification.NotFound", "Notification not found."));

        if (!notification.IsRead)
        {
            notification.MarkAsRead();
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
