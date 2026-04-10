using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Module.Billing.Domain.Entities;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.UpdatePlan;

internal sealed class UpdatePlanCommandHandler(
    IApplicationDbContext appContext,
    BillingDbContext billingContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdatePlanCommand, Result>
{
    public async Task<Result> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await billingContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (plan is null)
            return Result.Failure(BillingErrors.PlanNotFound);

        string? featuresJson = null;
        if (request.Features is { Count: > 0 })
        {
            var keys = request.Features.Select(f => f.Key).ToList();
            var existingKeys = await appContext.Set<FeatureFlag>()
                .AsNoTracking()
                .Where(f => keys.Contains(f.Key))
                .Select(f => f.Key)
                .ToListAsync(cancellationToken);

            var invalidKeys = keys.Except(existingKeys).ToList();
            if (invalidKeys.Count > 0)
                return Result.Failure(BillingErrors.InvalidFeatureKeys(invalidKeys));

            featuresJson = JsonSerializer.Serialize(request.Features);
        }

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

            billingContext.PlanPriceHistories.Add(priceHistory);
        }

        plan.Update(
            request.Name,
            request.Description,
            request.Translations,
            request.MonthlyPrice,
            request.AnnualPrice,
            featuresJson,
            request.IsPublic,
            request.DisplayOrder,
            request.TrialDays);

        await billingContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
