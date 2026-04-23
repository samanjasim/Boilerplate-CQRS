using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowHistory;

internal sealed class GetWorkflowHistoryQueryHandler(
    IWorkflowService workflowService,
    WorkflowDbContext workflowDbContext,
    ICurrentUserService currentUser) : IRequestHandler<GetWorkflowHistoryQuery, Result<List<WorkflowStepRecord>>>
{
    public async Task<Result<List<WorkflowStepRecord>>> Handle(
        GetWorkflowHistoryQuery request, CancellationToken cancellationToken)
    {
        // Access control: users without ViewAllTasks must be initiator or have/had a task
        if (!currentUser.HasPermission(WorkflowPermissions.ViewAllTasks))
        {
            var instance = await workflowDbContext.WorkflowInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.InstanceId, cancellationToken);

            if (instance is null)
                return Result.Failure<List<WorkflowStepRecord>>(WorkflowErrors.InstanceNotFound(request.InstanceId));

            if (instance.StartedByUserId != currentUser.UserId)
            {
                var hasTask = await workflowDbContext.ApprovalTasks
                    .AnyAsync(t => t.InstanceId == request.InstanceId
                        && t.AssigneeUserId == currentUser.UserId, cancellationToken);

                if (!hasTask)
                    return Result.Failure<List<WorkflowStepRecord>>(WorkflowErrors.InstanceNotFound(request.InstanceId));
            }
        }

        var history = await workflowService.GetHistoryAsync(
            request.InstanceId, cancellationToken);

        return Result.Success(history.ToList());
    }
}
