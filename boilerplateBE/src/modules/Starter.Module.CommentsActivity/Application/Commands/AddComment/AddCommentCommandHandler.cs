using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Errors;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.AddComment;

internal sealed class AddCommentCommandHandler(
    CommentsActivityDbContext context,
    ICommentableEntityRegistry registry,
    ICurrentUserService currentUser) : IRequestHandler<AddCommentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        if (!registry.IsCommentable(request.EntityType))
            return Result.Failure<Guid>(CommentErrors.NotCommentable(request.EntityType));

        if (request.ParentCommentId.HasValue)
        {
            var parent = await context.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentCommentId.Value, cancellationToken);

            if (parent is null)
                return Result.Failure<Guid>(CommentErrors.NotFound(request.ParentCommentId.Value));

            if (parent.ParentCommentId is not null)
                return Result.Failure<Guid>(CommentErrors.CannotReplyToReply);
        }

        string? mentionsJson = null;
        if (request.MentionUserIds is { Count: > 0 })
        {
            mentionsJson = JsonSerializer.Serialize(request.MentionUserIds);
        }

        var comment = Comment.Create(
            currentUser.TenantId,
            request.EntityType,
            request.EntityId,
            request.ParentCommentId,
            currentUser.UserId!.Value,
            request.Body,
            mentionsJson);

        context.Comments.Add(comment);

        if (request.AttachmentFileIds is { Count: > 0 })
        {
            for (var i = 0; i < request.AttachmentFileIds.Count; i++)
            {
                var attachment = CommentAttachment.Create(
                    comment.Id,
                    request.AttachmentFileIds[i],
                    i);
                context.CommentAttachments.Add(attachment);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(comment.Id);
    }
}
