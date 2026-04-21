using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowStatus;

internal sealed class GetWorkflowStatusQueryHandler(
    IWorkflowService workflowService) : IRequestHandler<GetWorkflowStatusQuery, Result<WorkflowStatusSummary?>>
{
    public async Task<Result<WorkflowStatusSummary?>> Handle(
        GetWorkflowStatusQuery request, CancellationToken cancellationToken)
    {
        var status = await workflowService.GetStatusAsync(
            request.EntityType, request.EntityId, cancellationToken);

        return Result.Success(status);
    }
}
