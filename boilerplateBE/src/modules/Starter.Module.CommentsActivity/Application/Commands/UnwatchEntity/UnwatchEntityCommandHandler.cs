using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.UnwatchEntity;

internal sealed class UnwatchEntityCommandHandler(
    CommentsActivityDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<UnwatchEntityCommand, Result>
{
    public async Task<Result> Handle(UnwatchEntityCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var watcher = await context.EntityWatchers
            .FirstOrDefaultAsync(
                w => w.EntityType == request.EntityType &&
                     w.EntityId == request.EntityId &&
                     w.UserId == userId,
                cancellationToken);

        if (watcher is not null)
        {
            context.EntityWatchers.Remove(watcher);
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
