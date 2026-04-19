using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowDefinitions;

internal sealed class GetWorkflowDefinitionsQueryHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<GetWorkflowDefinitionsQuery, Result<List<WorkflowDefinitionSummary>>>
{
    public async Task<Result<List<WorkflowDefinitionSummary>>> Handle(
        GetWorkflowDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var definitions = await workflowService.GetDefinitionsAsync(
            request.EntityType, currentUser.TenantId, cancellationToken);

        return Result.Success(definitions.ToList());
    }
}
