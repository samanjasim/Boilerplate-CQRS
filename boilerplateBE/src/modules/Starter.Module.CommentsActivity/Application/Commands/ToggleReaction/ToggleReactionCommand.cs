using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.ToggleReaction;

public sealed record ToggleReactionCommand(
    Guid CommentId,
    string ReactionType) : IRequest<Result>;
