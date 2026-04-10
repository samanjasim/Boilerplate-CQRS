using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPlans;

internal sealed class GetPlansQueryHandler(
    BillingDbContext context) : IRequestHandler<GetPlansQuery, Result<List<SubscriptionPlanDto>>>
{
    public async Task<Result<List<SubscriptionPlanDto>>> Handle(
        GetPlansQuery request, CancellationToken cancellationToken)
    {
        var query = context.SubscriptionPlans.AsNoTracking().AsQueryable();

        if (request.PublicOnly)
            query = query.Where(p => p.IsActive && p.IsPublic);
        else if (!request.IncludeInactive)
            query = query.Where(p => p.IsActive);

        query = query.OrderBy(p => p.DisplayOrder);

        var plans = await query.ToListAsync(cancellationToken);

        var planIds = plans.Select(p => p.Id).ToList();
        var subscriberCounts = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Active && planIds.Contains(s.SubscriptionPlanId))
            .GroupBy(s => s.SubscriptionPlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count, cancellationToken);

        var dtos = plans
            .Select(p => p.ToDto(subscriberCounts.GetValueOrDefault(p.Id, 0)))
            .ToList();

        return Result.Success(dtos);
    }
}
