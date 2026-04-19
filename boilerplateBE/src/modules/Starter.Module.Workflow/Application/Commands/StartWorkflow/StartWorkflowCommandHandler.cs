using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.StartWorkflow;

internal sealed class StartWorkflowCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<StartWorkflowCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
        var instanceId = await workflowService.StartAsync(
            request.EntityType,
            request.EntityId,
            request.DefinitionName,
            currentUser.UserId!.Value,
            currentUser.TenantId,
            cancellationToken);

        if (instanceId == Guid.Empty)
            return Result.Failure<Guid>(
                Domain.Errors.WorkflowErrors.DefinitionNotFound(request.DefinitionName));

        return Result.Success(instanceId);
    }
}
