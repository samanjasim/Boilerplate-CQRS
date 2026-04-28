using System.Text.Json;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.ApprovePendingAction;

internal sealed class ApprovePendingActionCommandHandler(
    IPendingApprovalService approvals,
    ISender sender,
    ICurrentUserService currentUser,
    AiDbContext db) : IRequestHandler<ApprovePendingActionCommand, Result<object?>>
{
    public async Task<Result<object?>> Handle(ApprovePendingActionCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<object?>(Error.Unauthorized());

        // Resolve the command type before flipping state — if missing, auto-deny.
        var paProbe = await db.AiPendingApprovals.FindAsync(new object?[] { cmd.ApprovalId }, ct);
        if (paProbe is null)
            return Result.Failure<object?>(PendingApprovalErrors.NotFound(cmd.ApprovalId));

        // Tenant scope — superadmin (TenantId == null) may approve any tenant's row.
        if (currentUser.TenantId is not null && paProbe.TenantId != currentUser.TenantId)
            return Result.Failure<object?>(PendingApprovalErrors.AccessDenied);

        var commandType = Type.GetType(paProbe.CommandTypeName, throwOnError: false);
        if (commandType is null)
        {
            await approvals.DenyAsync(cmd.ApprovalId, userId, $"tool unavailable: {paProbe.CommandTypeName}", ct);
            return Result.Failure<object?>(PendingApprovalErrors.ToolUnavailable(paProbe.CommandTypeName));
        }

        // Flip status to Approved (raises AgentApprovalApprovedEvent).
        var approveResult = await approvals.ApproveAsync(cmd.ApprovalId, userId, cmd.Note, ct);
        if (approveResult.IsFailure)
            return Result.Failure<object?>(approveResult.Error);

        // Reconstitute and re-dispatch via ApprovalGrantExecutionContext.
        var commandObject = JsonSerializer.Deserialize(
            paProbe.ArgumentsJson, commandType, AiJsonDefaults.Serializer);
        if (commandObject is null)
            return Result.Failure<object?>(PendingApprovalErrors.ToolUnavailable(paProbe.CommandTypeName));

        var ambient = AmbientExecutionContext.Current
            ?? new HttpExecutionContextStub(); // safety: should never be null inside an HTTP request
        using var grantScope = AmbientExecutionContext.Use(
            new ApprovalGrantExecutionContext(ambient));

        var toolResult = await sender.Send(commandObject, ct);
        return Result.Success(toolResult);
    }

    private sealed class HttpExecutionContextStub : IExecutionContext
    {
        public Guid? UserId => null;
        public Guid? AgentPrincipalId => null;
        public Guid? TenantId => null;
        public Guid? AgentRunId => null;
        public bool DangerousActionApprovalGrant => false;
        public bool HasPermission(string permission) => false;
    }
}
