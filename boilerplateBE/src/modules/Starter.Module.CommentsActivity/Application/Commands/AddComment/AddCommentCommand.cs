using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.AddComment;

public sealed record AddCommentCommand(
    string EntityType,
    Guid EntityId,
    string Body,
    List<Guid>? MentionUserIds,
    Guid? ParentCommentId,
    List<Guid>? AttachmentFileIds) : IRequest<Result<Guid>>;
