using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CancelWorkflow;

internal sealed class CancelWorkflowCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<CancelWorkflowCommand, Result>
{
    public async Task<Result> Handle(CancelWorkflowCommand request, CancellationToken cancellationToken)
    {
        await workflowService.CancelAsync(
            request.InstanceId,
            request.Reason,
            currentUser.UserId!.Value,
            cancellationToken);

        return Result.Success();
    }
}
