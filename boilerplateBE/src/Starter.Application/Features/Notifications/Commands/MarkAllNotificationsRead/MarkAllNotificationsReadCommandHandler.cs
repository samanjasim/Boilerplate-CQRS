using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

internal sealed class MarkAllNotificationsReadCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var now = DateTime.UtcNow;

        await context.Set<Notification>()
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), cancellationToken);

        return Result.Success();
    }
}
