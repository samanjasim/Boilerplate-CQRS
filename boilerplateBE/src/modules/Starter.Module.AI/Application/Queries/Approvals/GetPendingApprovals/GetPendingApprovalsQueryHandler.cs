using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovals;

internal sealed class GetPendingApprovalsQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetPendingApprovalsQuery, Result<PaginatedList<PendingApprovalDto>>>
{
    public async Task<Result<PaginatedList<PendingApprovalDto>>> Handle(
        GetPendingApprovalsQuery q, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<PaginatedList<PendingApprovalDto>>(Error.Unauthorized());

        // Tenant scope is enforced by the global query filter on AiPendingApproval.
        IQueryable<AiPendingApproval> source = db.AiPendingApprovals.AsNoTracking();

        // Permission scoping: ApproveAction sees all rows in the tenant; otherwise
        // (ViewApprovals only) caller sees only their own requests.
        var canApprove = currentUser.HasPermission(AiPermissions.AgentsApproveAction);
        if (!canApprove)
            source = source.Where(p => p.RequestingUserId == userId);

        if (q.Status is { } s) source = source.Where(p => p.Status == s);
        if (q.AssistantId is { } a) source = source.Where(p => p.AssistantId == a);

        source = source.OrderByDescending(p => p.CreatedAt);

        var page = q.Page < 1 ? 1 : q.Page;
        var size = q.PageSize is < 1 or > 200 ? 20 : q.PageSize;

        var total = await source.CountAsync(ct);
        var items = await source
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new PendingApprovalDto(
                p.Id,
                p.AssistantId,
                p.AssistantName,
                p.ToolName,
                p.CommandTypeName,
                p.ArgumentsJson,
                p.ReasonHint,
                p.Status,
                p.RequestingUserId,
                p.DecisionUserId,
                p.DecisionReason,
                p.DecidedAt,
                p.ExpiresAt,
                p.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PaginatedList<PendingApprovalDto>(items, total, page, size));
    }
}
