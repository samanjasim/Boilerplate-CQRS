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

        var plan = SubscriptionPlan.Create(
            request.Name,
            request.Slug,
            request.Description,
            request.Translations,
            request.MonthlyPrice,
            request.AnnualPrice,
            request.Currency,
            request.Features,
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
