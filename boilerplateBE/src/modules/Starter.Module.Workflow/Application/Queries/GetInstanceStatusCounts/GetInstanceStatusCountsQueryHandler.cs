using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;

internal sealed class GetInstanceStatusCountsQueryHandler(
    WorkflowDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetInstanceStatusCountsQuery, Result<InstanceStatusCountsDto>>
{
    public async Task<Result<InstanceStatusCountsDto>> Handle(
        GetInstanceStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var startedByUserId = request.StartedByUserId;
        if (!currentUser.HasPermission(WorkflowPermissions.ViewAllTasks))
        {
            if (currentUser.UserId is null)
                return Result.Failure<InstanceStatusCountsDto>(Error.Unauthorized());

            startedByUserId = currentUser.UserId;
        }

        var instances = db.WorkflowInstances.AsNoTracking().AsQueryable();

        if (startedByUserId is { } userId)
            instances = instances.Where(i => i.StartedByUserId == userId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            instances = instances.Where(i => i.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.State))
            instances = instances.Where(i => i.CurrentState == request.State);

        var statusGroups = await instances
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusCounts = statusGroups.ToDictionary(x => x.Status, x => x.Count);
        var awaiting = await instances
            .Where(i => i.Status == InstanceStatus.Active)
            .Where(i => db.ApprovalTasks.Any(
                t => t.InstanceId == i.Id && t.Status == TaskStatus.Pending))
            .CountAsync(cancellationToken);

        var totalActive = statusCounts.GetValueOrDefault(InstanceStatus.Active);

        return Result.Success(new InstanceStatusCountsDto(
            Active: totalActive - awaiting,
            Awaiting: awaiting,
            Completed: statusCounts.GetValueOrDefault(InstanceStatus.Completed),
            Cancelled: statusCounts.GetValueOrDefault(InstanceStatus.Cancelled)));
    }
}
