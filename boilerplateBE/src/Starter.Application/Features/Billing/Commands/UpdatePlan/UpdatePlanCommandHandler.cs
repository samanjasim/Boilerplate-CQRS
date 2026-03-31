using Microsoft.EntityFrameworkCore;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.UpdatePlan;

internal sealed class UpdatePlanCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<UpdatePlanCommand, Result>
{
    public async Task<Result> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (plan is null)
            return Result.Failure(BillingErrors.PlanNotFound);

        var priceChanged = plan.MonthlyPrice != request.MonthlyPrice
            || plan.AnnualPrice != request.AnnualPrice;

        if (priceChanged)
        {
            var priceHistory = PlanPriceHistory.Create(
                plan.Id,
                plan.MonthlyPrice,
                plan.AnnualPrice,
                plan.Currency,
                currentUser.UserId ?? Guid.Empty,
                request.PriceChangeReason);

            context.PlanPriceHistories.Add(priceHistory);
        }

        plan.Update(
            request.Name,
            request.Description,
            request.Translations,
            request.MonthlyPrice,
            request.AnnualPrice,
            request.Features,
            request.IsPublic,
            request.DisplayOrder,
            request.TrialDays);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
