using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Domain.Constants;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowInstanceById;

internal sealed class GetWorkflowInstanceByIdQueryHandler(
    WorkflowDbContext db,
    IUserReader userReader,
    ICurrentUserService currentUser) : IRequestHandler<GetWorkflowInstanceByIdQuery, Result<WorkflowInstanceSummary>>
{
    private readonly IUserReader _userReader = userReader;

    public async Task<Result<WorkflowInstanceSummary>> Handle(
        GetWorkflowInstanceByIdQuery request, CancellationToken cancellationToken)
    {
        var instance = await db.WorkflowInstances
            .Include(i => i.Definition)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.InstanceId, cancellationToken);

        if (instance is null)
            return Result.Failure<WorkflowInstanceSummary>(WorkflowErrors.InstanceNotFound(request.InstanceId));

        // Mirror GetWorkflowHistory's access check: ViewAllTasks holders see every
        // instance, initiators see their own, AND anyone with a past/current task
        // on the instance can see it (otherwise an approver can open the history
        // timeline but gets 404 on the detail page). Return NotFound (not Forbid)
        // to avoid leaking existence of instances belonging to other users.
        if (!currentUser.HasPermission(WorkflowPermissions.ViewAllTasks)
            && instance.StartedByUserId != currentUser.UserId)
        {
            var hasTask = await db.ApprovalTasks
                .AsNoTracking()
                .AnyAsync(t => t.InstanceId == request.InstanceId
                               && (t.AssigneeUserId == currentUser.UserId
                                   || t.OriginalAssigneeUserId == currentUser.UserId
                                   || t.CompletedByUserId == currentUser.UserId),
                          cancellationToken);

            if (!hasTask)
                return Result.Failure<WorkflowInstanceSummary>(WorkflowErrors.InstanceNotFound(request.InstanceId));
        }

        var users = await _userReader.GetManyAsync([instance.StartedByUserId], cancellationToken);
        var displayName = users.FirstOrDefault(u => u.Id == instance.StartedByUserId)?.DisplayName;

        var canResubmit = false;
        if (instance.Status == InstanceStatus.Active)
        {
            try
            {
                var states = System.Text.Json.JsonSerializer
                    .Deserialize<List<WorkflowStateConfig>>(instance.Definition.StatesJson);
                var cur = states?.FirstOrDefault(s => s.Name == instance.CurrentState);
                canResubmit = cur is not null
                    && cur.Type.Equals(WorkflowStateTypes.Initial, StringComparison.OrdinalIgnoreCase);
            }
            catch { /* swallow */ }
        }

        return Result.Success(new WorkflowInstanceSummary(
            instance.Id,
            instance.DefinitionId,
            instance.Definition.Name,
            instance.EntityType,
            instance.EntityId,
            instance.CurrentState,
            instance.Status.ToString(),
            instance.StartedAt,
            instance.CompletedAt,
            instance.StartedByUserId,
            displayName,
            instance.EntityDisplayName,
            canResubmit));
    }
}
