using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.DeactivatePlan;

internal sealed class DeactivatePlanCommandHandler(
    BillingDbContext context) : IRequestHandler<DeactivatePlanCommand, Result>
{
    public async Task<Result> Handle(DeactivatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (plan is null)
            return Result.Failure(BillingErrors.PlanNotFound);

        if (plan.IsFree)
        {
            var otherActiveFreeExists = await context.SubscriptionPlans
                .AnyAsync(p => p.Id != request.Id && p.IsFree && p.IsActive, cancellationToken);
            if (!otherActiveFreeExists)
                return Result.Failure(BillingErrors.FreePlanRequired);
        }

        plan.Deactivate();
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
