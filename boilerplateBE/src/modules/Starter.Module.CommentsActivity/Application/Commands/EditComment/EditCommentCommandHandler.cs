using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Errors;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.EditComment;

internal sealed class EditCommentCommandHandler(
    CommentsActivityDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<EditCommentCommand, Result>
{
    public async Task<Result> Handle(EditCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await context.Comments
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (comment is null)
            return Result.Failure(CommentErrors.NotFound(request.Id));

        if (comment.IsDeleted)
            return Result.Failure(CommentErrors.AlreadyDeleted);

        if (comment.AuthorId != currentUser.UserId!.Value &&
            !currentUser.HasPermission(Constants.CommentsActivityPermissions.ManageComments))
        {
            return Result.Failure(CommentErrors.NotAuthor);
        }

        string? mentionsJson = null;
        if (request.MentionUserIds is { Count: > 0 })
        {
            mentionsJson = JsonSerializer.Serialize(request.MentionUserIds);
        }

        comment.Edit(request.Body, mentionsJson, currentUser.UserId!.Value);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
