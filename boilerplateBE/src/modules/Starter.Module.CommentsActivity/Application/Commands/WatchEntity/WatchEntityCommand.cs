using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.WatchEntity;

public sealed record WatchEntityCommand(
    string EntityType,
    Guid EntityId) : IRequest<Result>;
