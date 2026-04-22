using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Common;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.ExecuteTask;

internal sealed class ExecuteTaskCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<ExecuteTaskCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ExecuteTaskCommand request, CancellationToken cancellationToken)
    {
        var wfResult = await workflowService.ExecuteTaskAsync(
            request.TaskId,
            request.Action,
            request.Comment,
            currentUser.UserId!.Value,
            request.FormData,
            cancellationToken);

        return WorkflowTaskResultAdapter.ToResult(wfResult);
    }
}
