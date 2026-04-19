using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowHistory;

internal sealed class GetWorkflowHistoryQueryHandler(
    IWorkflowService workflowService) : IRequestHandler<GetWorkflowHistoryQuery, Result<List<WorkflowStepRecord>>>
{
    public async Task<Result<List<WorkflowStepRecord>>> Handle(
        GetWorkflowHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await workflowService.GetHistoryAsync(
            request.InstanceId, cancellationToken);

        return Result.Success(history.ToList());
    }
}
