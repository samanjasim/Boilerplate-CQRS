using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CancelWorkflow;

internal sealed class CancelWorkflowCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<CancelWorkflowCommand, Result>
{
    public async Task<Result> Handle(CancelWorkflowCommand request, CancellationToken cancellationToken)
    {
        var cancelled = await workflowService.CancelAsync(
            request.InstanceId,
            request.Reason,
            currentUser.UserId!.Value,
            cancellationToken);

        // The engine returns false both for "not found" and "not active" —
        // surface that as NotFound so we don't leak the existence of a
        // foreign tenant's finished/cancelled instances.
        if (!cancelled)
            return Result.Failure(WorkflowErrors.InstanceNotFound(request.InstanceId));

        return Result.Success();
    }
}
