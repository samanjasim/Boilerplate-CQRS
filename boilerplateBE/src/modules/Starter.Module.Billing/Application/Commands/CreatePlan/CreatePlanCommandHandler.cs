using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Module.Billing.Domain.Entities;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.CreatePlan;

internal sealed class CreatePlanCommandHandler(
    IApplicationDbContext appContext,
    BillingDbContext billingContext,
    ICurrentUserService currentUser) : IRequestHandler<CreatePlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await billingContext.SubscriptionPlans
            .AnyAsync(p => p.Slug == request.Slug.Trim().ToLowerInvariant(), cancellationToken);
        if (slugExists)
            return Result.Failure<Guid>(BillingErrors.SlugAlreadyExists);

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
                return Result.Failure<Guid>(BillingErrors.InvalidFeatureKeys(invalidKeys));

            featuresJson = JsonSerializer.Serialize(request.Features);
        }

        var plan = SubscriptionPlan.Create(
            request.Name,
            request.Slug,
            request.Description,
            request.Translations,
            request.MonthlyPrice,
            request.AnnualPrice,
            request.Currency,
            featuresJson,
            request.IsFree,
            request.IsPublic,
            request.DisplayOrder,
            request.TrialDays);

        billingContext.SubscriptionPlans.Add(plan);

        var priceHistory = PlanPriceHistory.Create(
            plan.Id,
            request.MonthlyPrice,
            request.AnnualPrice,
            request.Currency,
            currentUser.UserId ?? Guid.Empty,
            "Initial plan creation");

        billingContext.PlanPriceHistories.Add(priceHistory);

        await billingContext.SaveChangesAsync(cancellationToken);
        return Result.Success(plan.Id);
    }
}
