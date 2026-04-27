using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Approvals;

internal sealed class PendingApprovalService(
    AiDbContext db,
    ILogger<PendingApprovalService> logger) : IPendingApprovalService
{
    public async Task<AiPendingApproval> CreateAsync(
        Guid? tenantId, Guid assistantId, string assistantName, Guid agentPrincipalId,
        Guid? conversationId, Guid? agentTaskId, Guid? requestingUserId,
        string toolName, string commandTypeName, string argumentsJson,
        string? reasonHint, TimeSpan expiresIn, CancellationToken ct)
    {
        var entity = AiPendingApproval.Create(
            tenantId: tenantId,
            assistantId: assistantId,
            assistantName: assistantName,
            agentPrincipalId: agentPrincipalId,
            conversationId: conversationId,
            agentTaskId: agentTaskId,
            requestingUserId: requestingUserId,
            toolName: toolName,
            commandTypeName: commandTypeName,
            argumentsJson: argumentsJson,
            reasonHint: reasonHint,
            expiresAt: DateTime.UtcNow.Add(expiresIn));

        db.AiPendingApprovals.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Result<AiPendingApproval>> ApproveAsync(
        Guid approvalId, Guid decisionUserId, string? reason, CancellationToken ct)
    {
        var entity = await db.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == approvalId, ct);
        if (entity is null)
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotFound(approvalId));
        if (!entity.TryApprove(decisionUserId, reason))
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotPending);

        await db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }

    public async Task<Result<AiPendingApproval>> DenyAsync(
        Guid approvalId, Guid decisionUserId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.DenyReasonRequired);

        var entity = await db.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == approvalId, ct);
        if (entity is null)
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotFound(approvalId));
        if (!entity.TryDeny(decisionUserId, reason))
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotPending);

        await db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }

    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken ct)
    {
        // Atomic claim with FOR UPDATE SKIP LOCKED — multi-replica safe by construction.
        // We hydrate the matching entities (limit batchSize), call TryExpire to raise events,
        // then SaveChanges. Rows that another replica already grabbed are skipped.
        // Wrap SELECT FOR UPDATE + SaveChanges in an explicit transaction so the row-lock
        // is held until the state mutation commits — atomic by contract, not by autocommit.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var due = await db.AiPendingApprovals
            .FromSqlRaw(
                """
                SELECT * FROM ai_pending_approvals
                WHERE status = 0 AND expires_at < now()
                ORDER BY expires_at ASC
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """, batchSize)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            await tx.CommitAsync(ct);
            return 0;
        }

        var expired = 0;
        foreach (var row in due)
            if (row.TryExpire()) expired++;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Expired {Count} pending approvals.", expired);
        return expired;
    }
}
