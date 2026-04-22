using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

internal sealed class BatchExecuteTasksCommandHandler(
    IWorkflowService workflowService,
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    ILogger<BatchExecuteTasksCommandHandler> logger)
    : IRequestHandler<BatchExecuteTasksCommand, Result<BatchExecuteResult>>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<BatchExecuteResult>> Handle(
        BatchExecuteTasksCommand request, CancellationToken cancellationToken)
    {
        var outcomes = new List<BatchItemOutcome>(request.TaskIds.Count);
        var userId = currentUser.UserId!.Value;

        // Pre-load state configs for all target tasks so we can detect which ones
        // require per-task form data (bulk actions cannot provide it). These are
        // surfaced as "Skipped" with a clear message instead of a generic failure.
        var stateConfigs = await LoadCurrentStateConfigsAsync(request.TaskIds, cancellationToken);

        foreach (var taskId in request.TaskIds)
        {
            if (stateConfigs.TryGetValue(taskId, out var state)
                && state?.FormFields is { Count: > 0 }
                && state.FormFields.Any(f => f.Required))
            {
                outcomes.Add(new BatchItemOutcome(
                    taskId,
                    "Skipped",
                    "Task requires form data that cannot be supplied in a bulk action. Open the task to fill the form."));
                continue;
            }

            try
            {
                var wfResult = await workflowService.ExecuteTaskAsync(
                    taskId,
                    request.Action,
                    request.Comment,
                    userId,
                    formData: null,
                    cancellationToken);
                var ok = wfResult.IsSuccess;

                outcomes.Add(ok
                    ? new BatchItemOutcome(taskId, "Succeeded", null)
                    : new BatchItemOutcome(taskId, "Failed", "Task could not be executed (not found, not pending, or unauthorized)."));
            }
            catch (Exception ex)
            {
                // Log full exception for diagnostics but never surface internals to callers.
                logger.LogError(ex, "Bulk task execution failed for task {TaskId}.", taskId);
                outcomes.Add(new BatchItemOutcome(taskId, "Failed", "An unexpected error occurred while executing this task."));
            }
        }

        var result = new BatchExecuteResult(
            Succeeded: outcomes.Count(o => o.Status == "Succeeded"),
            Failed: outcomes.Count(o => o.Status == "Failed"),
            Skipped: outcomes.Count(o => o.Status == "Skipped"),
            Items: outcomes);

        return Result.Success(result);
    }

    private async Task<Dictionary<Guid, WorkflowStateConfig?>> LoadCurrentStateConfigsAsync(
        IReadOnlyList<Guid> taskIds, CancellationToken ct)
    {
        var rows = await dbContext.ApprovalTasks
            .AsNoTracking()
            .Where(t => taskIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                CurrentState = t.Instance.CurrentState,
                StatesJson = t.Instance.Definition.StatesJson,
            })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, WorkflowStateConfig?>(rows.Count);
        foreach (var row in rows)
        {
            var states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(row.StatesJson, JsonOpts) ?? [];
            result[row.Id] = states.FirstOrDefault(s => s.Name == row.CurrentState);
        }
        return result;
    }
}
