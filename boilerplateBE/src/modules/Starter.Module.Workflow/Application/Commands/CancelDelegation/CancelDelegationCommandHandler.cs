using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CancelDelegation;

internal sealed class CancelDelegationCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    ILogger<CancelDelegationCommandHandler> logger) : IRequestHandler<CancelDelegationCommand, Result>
{
    public async Task<Result> Handle(CancelDelegationCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var rule = await dbContext.DelegationRules
            .FirstOrDefaultAsync(r => r.Id == request.DelegationId, cancellationToken);

        if (rule is null)
            return Result.Failure(Error.NotFound(
                "Delegation.NotFound", "Delegation rule not found."));

        if (rule.FromUserId != userId)
            return Result.Failure(Error.Forbidden(
                "You can only cancel your own delegation rules."));

        rule.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Delegation {DelegationId} cancelled by user {UserId}", request.DelegationId, userId);

        return Result.Success();
    }
}
