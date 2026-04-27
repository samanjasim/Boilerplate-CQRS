using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Errors;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;

internal sealed class DenyPendingActionCommandHandler(
    IPendingApprovalService approvals,
    ICurrentUserService currentUser) : IRequestHandler<DenyPendingActionCommand, Result>
{
    public async Task<Result> Handle(DenyPendingActionCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized());
        if (string.IsNullOrWhiteSpace(cmd.Reason))
            return Result.Failure(PendingApprovalErrors.DenyReasonRequired);

        var result = await approvals.DenyAsync(cmd.ApprovalId, userId, cmd.Reason, ct);
        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }
}
