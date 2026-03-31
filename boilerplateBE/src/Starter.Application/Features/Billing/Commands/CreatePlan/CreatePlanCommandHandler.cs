using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.CreatePlan;

internal sealed class CreatePlanCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<CreatePlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await context.SubscriptionPlans
            .AnyAsync(p => p.Slug == request.Slug.Trim().ToLowerInvariant(), cancellationToken);
        if (slugExists)
            return Result.Failure<Guid>(BillingErrors.SlugAlreadyExists);

        string? featuresJson = null;
        if (request.Features is { Count: > 0 })
        {
            var keys = request.Features.Select(f => f.Key).ToList();
            var existingKeys = await context.FeatureFlags
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

        context.SubscriptionPlans.Add(plan);

        var priceHistory = PlanPriceHistory.Create(
            plan.Id,
            request.MonthlyPrice,
            request.AnnualPrice,
            request.Currency,
            currentUser.UserId ?? Guid.Empty,
            "Initial plan creation");

        context.PlanPriceHistories.Add(priceHistory);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(plan.Id);
    }
}
