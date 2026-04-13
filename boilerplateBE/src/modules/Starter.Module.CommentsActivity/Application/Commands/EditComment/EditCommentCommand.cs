using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.EditComment;

public sealed record EditCommentCommand(
    Guid Id,
    string Body,
    List<Guid>? MentionUserIds) : IRequest<Result>;
