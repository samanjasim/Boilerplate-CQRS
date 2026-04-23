using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowInstances;

internal sealed class GetWorkflowInstancesQueryHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<GetWorkflowInstancesQuery, Result<List<WorkflowInstanceSummary>>>
{
    public async Task<Result<List<WorkflowInstanceSummary>>> Handle(
        GetWorkflowInstancesQuery request, CancellationToken cancellationToken)
    {
        // Server-side user scoping: users without ViewAllTasks permission
        // can only see their own workflow instances, regardless of what the
        // frontend passes as startedByUserId.
        var startedByUserId = request.StartedByUserId;

        if (!currentUser.HasPermission(WorkflowPermissions.ViewAllTasks))
        {
            startedByUserId = currentUser.UserId;
        }

        var instances = await workflowService.GetInstancesAsync(
            request.EntityType, request.State, startedByUserId, request.Status,
            request.Page, request.PageSize, cancellationToken);

        return Result.Success(instances.ToList());
    }
}
