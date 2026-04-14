using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Errors;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.ToggleReaction;

internal sealed class ToggleReactionCommandHandler(
    CommentsActivityDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<ToggleReactionCommand, Result>
{
    public async Task<Result> Handle(ToggleReactionCommand request, CancellationToken cancellationToken)
    {
        var comment = await context.Comments
            .FirstOrDefaultAsync(c => c.Id == request.CommentId, cancellationToken);

        if (comment is null)
            return Result.Failure(CommentErrors.NotFound(request.CommentId));

        var userId = currentUser.UserId!.Value;
        var reactionType = request.ReactionType.Trim();

        var existing = await context.CommentReactions
            .FirstOrDefaultAsync(
                r => r.CommentId == request.CommentId &&
                     r.UserId == userId &&
                     r.ReactionType == reactionType,
                cancellationToken);

        bool added;
        if (existing is not null)
        {
            context.CommentReactions.Remove(existing);
            added = false;
        }
        else
        {
            var reaction = CommentReaction.Create(request.CommentId, userId, reactionType);
            context.CommentReactions.Add(reaction);
            added = true;
        }

        // Raise domain event through the aggregate root — dispatched by the interceptor on SaveChanges
        comment.RecordReactionToggle(reactionType, added);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
