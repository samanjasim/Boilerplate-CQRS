using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovalById;

internal sealed class GetPendingApprovalByIdQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetPendingApprovalByIdQuery, Result<PendingApprovalDto>>
{
    public async Task<Result<PendingApprovalDto>> Handle(
        GetPendingApprovalByIdQuery q, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<PendingApprovalDto>(Error.Unauthorized());

        // Tenant scope is enforced by the global query filter on AiPendingApproval.
        var entity = await db.AiPendingApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == q.ApprovalId, ct);

        if (entity is null)
            return Result.Failure<PendingApprovalDto>(PendingApprovalErrors.NotFound(q.ApprovalId));

        // Per-row permission scoping mirrors GetPendingApprovalsQueryHandler:
        // callers without ApproveAction can only see rows they requested.
        var canApprove = currentUser.HasPermission(AiPermissions.AgentsApproveAction);
        if (!canApprove && entity.RequestingUserId != userId)
            return Result.Failure<PendingApprovalDto>(PendingApprovalErrors.AccessDenied);

        return Result.Success(new PendingApprovalDto(
            entity.Id,
            entity.AssistantId,
            entity.AssistantName,
            entity.ToolName,
            entity.CommandTypeName,
            entity.ArgumentsJson,
            entity.ReasonHint,
            entity.Status,
            entity.RequestingUserId,
            entity.DecisionUserId,
            entity.DecisionReason,
            entity.DecidedAt,
            entity.ExpiresAt,
            entity.CreatedAt));
    }
}
