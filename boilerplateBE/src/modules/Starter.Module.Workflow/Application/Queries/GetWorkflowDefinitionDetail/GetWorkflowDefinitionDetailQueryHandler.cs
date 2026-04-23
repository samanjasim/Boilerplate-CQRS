using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowDefinitionDetail;

internal sealed class GetWorkflowDefinitionDetailQueryHandler(
    IWorkflowService workflowService) : IRequestHandler<GetWorkflowDefinitionDetailQuery, Result<WorkflowDefinitionDetail?>>
{
    public async Task<Result<WorkflowDefinitionDetail?>> Handle(
        GetWorkflowDefinitionDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await workflowService.GetDefinitionAsync(
            request.DefinitionId, cancellationToken);

        if (detail is null)
            return Result.Failure<WorkflowDefinitionDetail?>(
                WorkflowErrors.DefinitionNotFoundById(request.DefinitionId));

        return Result.Success<WorkflowDefinitionDetail?>(detail);
    }
}
