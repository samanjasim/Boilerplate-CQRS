using Microsoft.EntityFrameworkCore;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Encapsulates the decision logic for parallel-group approval tasks.
/// Given the completing task + the group's parallel mode + the action being taken,
/// returns whether the workflow should proceed with the transition or wait.
/// Side-effect: may cancel sibling tasks (AnyOf winner, or AllOf reject short-circuit).
/// </summary>
public sealed class ParallelApprovalCoordinator(WorkflowDbContext context)
{
    public async Task<ParallelDecision> EvaluateAsync(
        ApprovalTask task, string parallelMode, string action, CancellationToken ct)
    {
        if (!task.GroupId.HasValue)
            return ParallelDecision.Proceed;

        var siblings = await context.ApprovalTasks
            .Where(t => t.GroupId == task.GroupId && t.Id != task.Id)
            .ToListAsync(ct);

        if (parallelMode.Equals("AnyOf", StringComparison.OrdinalIgnoreCase))
        {
            // AnyOf: first completion wins — cancel all remaining siblings.
            foreach (var sibling in siblings.Where(s => s.Status == TaskStatus.Pending))
                sibling.Cancel();
            return ParallelDecision.Proceed;
        }

        // AllOf
        if (action.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            // Any rejection in AllOf: cancel remaining, transition proceeds to rejection state.
            foreach (var sibling in siblings.Where(s => s.Status == TaskStatus.Pending))
                sibling.Cancel();
            return ParallelDecision.Proceed;
        }

        // Approve in AllOf: only proceed when all siblings are Completed.
        var allComplete = siblings.All(s => s.Status == TaskStatus.Completed);
        return allComplete ? ParallelDecision.Proceed : ParallelDecision.Wait;
    }
}

public readonly record struct ParallelDecision(bool ShouldProceed)
{
    public static readonly ParallelDecision Proceed = new(true);
    public static readonly ParallelDecision Wait = new(false);
}
