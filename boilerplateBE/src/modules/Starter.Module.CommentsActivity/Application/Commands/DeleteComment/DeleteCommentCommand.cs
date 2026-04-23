using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.DeleteComment;

public sealed record DeleteCommentCommand(Guid Id) : IRequest<Result>;
