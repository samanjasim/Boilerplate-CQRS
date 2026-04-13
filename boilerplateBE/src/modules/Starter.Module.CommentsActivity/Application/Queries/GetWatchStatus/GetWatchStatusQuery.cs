using MediatR;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetWatchStatus;

public sealed record GetWatchStatusQuery(
    string EntityType,
    Guid EntityId) : IRequest<Result<WatchStatusDto>>;
