using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetPendingTasks;

public sealed record GetPendingTasksQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<Result<List<PendingTaskSummary>>>;
