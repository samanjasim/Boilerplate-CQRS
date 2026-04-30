using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;

public sealed record GetInstanceStatusCountsQuery(
    Guid? StartedByUserId = null,
    string? EntityType = null,
    string? State = null) : IRequest<Result<InstanceStatusCountsDto>>;
