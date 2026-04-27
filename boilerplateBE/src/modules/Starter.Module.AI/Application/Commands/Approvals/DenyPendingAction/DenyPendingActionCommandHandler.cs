using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;

internal sealed class DenyPendingActionCommandHandler(
    IPendingApprovalService approvals,
    ICurrentUserService currentUser,
    AiDbContext db) : IRequestHandler<DenyPendingActionCommand, Result>
{
    public async Task<Result> Handle(DenyPendingActionCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized());
        if (string.IsNullOrWhiteSpace(cmd.Reason))
            return Result.Failure(PendingApprovalErrors.DenyReasonRequired);

        // Defense-in-depth tenant-scope pre-check (mirrors ApprovePendingActionCommandHandler).
        // FirstOrDefaultAsync honors the EF tenant query filter; the explicit TenantId compare
        // protects against a future refactor to FindAsync (which bypasses query filters).
        var paProbe = await db.AiPendingApprovals
            .FirstOrDefaultAsync(p => p.Id == cmd.ApprovalId, ct);
        if (paProbe is null)
            return Result.Failure(PendingApprovalErrors.NotFound(cmd.ApprovalId));

        // Tenant scope — superadmin (TenantId == null) may deny any tenant's row.
        if (currentUser.TenantId is not null && paProbe.TenantId != currentUser.TenantId)
            return Result.Failure(PendingApprovalErrors.AccessDenied);

        var result = await approvals.DenyAsync(cmd.ApprovalId, userId, cmd.Reason, ct);
        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }
}
