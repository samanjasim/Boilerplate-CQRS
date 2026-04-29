using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;

internal sealed class GetInboxStatusCountsQueryHandler(
    WorkflowDbContext db,
    ICurrentUserService currentUser,
    TimeProvider clock) : IRequestHandler<GetInboxStatusCountsQuery, Result<InboxStatusCountsDto>>
{
    public async Task<Result<InboxStatusCountsDto>> Handle(
        GetInboxStatusCountsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result.Failure<InboxStatusCountsDto>(Error.Unauthorized());

        var now = clock.GetUtcNow().UtcDateTime;
        var tomorrow = now.Date.AddDays(1);

        var pendingTasks = db.ApprovalTasks
            .AsNoTracking()
            .Where(t => t.Status == TaskStatus.Pending)
            .Where(t => t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId);

        var totalPending = await pendingTasks.CountAsync(cancellationToken);
        var overdue = await pendingTasks
            .CountAsync(t => t.DueDate.HasValue && t.DueDate.Value < now, cancellationToken);
        var dueToday = await pendingTasks
            .CountAsync(t => t.DueDate.HasValue && t.DueDate.Value >= now && t.DueDate.Value < tomorrow, cancellationToken);

        return Result.Success(new InboxStatusCountsDto(
            Overdue: overdue,
            DueToday: dueToday,
            Upcoming: totalPending - overdue - dueToday));
    }
}
