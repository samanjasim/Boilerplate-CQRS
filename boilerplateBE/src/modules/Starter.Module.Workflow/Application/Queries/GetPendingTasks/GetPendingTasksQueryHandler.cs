using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetPendingTasks;

internal sealed class GetPendingTasksQueryHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<GetPendingTasksQuery, Result<List<PendingTaskSummary>>>
{
    public async Task<Result<List<PendingTaskSummary>>> Handle(
        GetPendingTasksQuery request, CancellationToken cancellationToken)
    {
        var tasks = await workflowService.GetPendingTasksAsync(
            currentUser.UserId!.Value, cancellationToken);

        // Apply simple paging over the in-memory list
        var paged = tasks
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result.Success(paged);
    }
}
