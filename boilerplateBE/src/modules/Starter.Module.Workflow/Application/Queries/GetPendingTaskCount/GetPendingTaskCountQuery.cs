using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetPendingTaskCount;

public sealed record GetPendingTaskCountQuery : IRequest<Result<int>>;
