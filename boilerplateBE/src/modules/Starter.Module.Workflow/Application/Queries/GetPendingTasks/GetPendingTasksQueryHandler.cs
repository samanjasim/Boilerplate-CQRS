using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetPendingTasks;

internal sealed class GetPendingTasksQueryHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<GetPendingTasksQuery, Result<PagedResult<PendingTaskSummary>>>
{
    public async Task<Result<PagedResult<PendingTaskSummary>>> Handle(
        GetPendingTasksQuery request, CancellationToken cancellationToken)
    {
        var paged = await workflowService.GetPendingTasksAsync(
            currentUser.UserId!.Value, request.Page, request.PageSize, cancellationToken);

        return Result.Success(paged);
    }
}
