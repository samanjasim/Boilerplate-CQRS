using Starter.Module.AI.Domain.Entities;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Approvals;

internal interface IPendingApprovalService
{
    Task<AiPendingApproval> CreateAsync(
        Guid? tenantId,
        Guid assistantId,
        string assistantName,
        Guid agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? requestingUserId,
        string toolName,
        string commandTypeName,
        string argumentsJson,
        string? reasonHint,
        TimeSpan expiresIn,
        CancellationToken ct);

    Task<Result<AiPendingApproval>> ApproveAsync(Guid approvalId, Guid decisionUserId, string? reason, CancellationToken ct);
    Task<Result<AiPendingApproval>> DenyAsync(Guid approvalId, Guid decisionUserId, string reason, CancellationToken ct);
    Task<int> ExpireDueAsync(int batchSize, CancellationToken ct);
}
