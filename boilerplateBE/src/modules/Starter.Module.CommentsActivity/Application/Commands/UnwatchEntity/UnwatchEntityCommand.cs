using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.UnwatchEntity;

public sealed record UnwatchEntityCommand(
    string EntityType,
    Guid EntityId) : IRequest<Result>;
