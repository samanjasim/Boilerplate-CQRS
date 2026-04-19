using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.TransitionWorkflow;

internal sealed class TransitionWorkflowCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<TransitionWorkflowCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(TransitionWorkflowCommand request, CancellationToken cancellationToken)
    {
        var success = await workflowService.TransitionAsync(
            request.InstanceId,
            request.Trigger,
            currentUser.UserId!.Value,
            cancellationToken);

        if (!success)
            return Result.Failure<bool>(WorkflowErrors.InvalidTransition("Initial", request.Trigger));

        return Result.Success(true);
    }
}
