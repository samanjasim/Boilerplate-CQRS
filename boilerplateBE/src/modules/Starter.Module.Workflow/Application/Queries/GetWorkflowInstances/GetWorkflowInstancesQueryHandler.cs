using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowInstances;

internal sealed class GetWorkflowInstancesQueryHandler(
    IWorkflowService workflowService) : IRequestHandler<GetWorkflowInstancesQuery, Result<List<WorkflowInstanceSummary>>>
{
    public async Task<Result<List<WorkflowInstanceSummary>>> Handle(
        GetWorkflowInstancesQuery request, CancellationToken cancellationToken)
    {
        var instances = await workflowService.GetInstancesAsync(
            request.EntityType, request.State, request.StartedByUserId, request.Status,
            request.Page, request.PageSize, cancellationToken);

        return Result.Success(instances.ToList());
    }
}
