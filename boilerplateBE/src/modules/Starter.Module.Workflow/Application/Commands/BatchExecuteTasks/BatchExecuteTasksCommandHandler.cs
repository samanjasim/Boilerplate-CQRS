using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

internal sealed class BatchExecuteTasksCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser)
    : IRequestHandler<BatchExecuteTasksCommand, Result<BatchExecuteResult>>
{
    public async Task<Result<BatchExecuteResult>> Handle(
        BatchExecuteTasksCommand request, CancellationToken cancellationToken)
    {
        var outcomes = new List<BatchItemOutcome>(request.TaskIds.Count);
        var userId = currentUser.UserId!.Value;

        foreach (var taskId in request.TaskIds)
        {
            try
            {
                var ok = await workflowService.ExecuteTaskAsync(
                    taskId,
                    request.Action,
                    request.Comment,
                    userId,
                    formData: null,
                    cancellationToken);

                outcomes.Add(ok
                    ? new BatchItemOutcome(taskId, "Succeeded", null)
                    : new BatchItemOutcome(taskId, "Failed", "Task could not be executed (not found, not pending, or unauthorized)."));
            }
            catch (Exception ex)
            {
                outcomes.Add(new BatchItemOutcome(taskId, "Failed", ex.Message));
            }
        }

        var result = new BatchExecuteResult(
            Succeeded: outcomes.Count(o => o.Status == "Succeeded"),
            Failed: outcomes.Count(o => o.Status == "Failed"),
            Skipped: outcomes.Count(o => o.Status == "Skipped"),
            Items: outcomes);

        return Result.Success(result);
    }
}
