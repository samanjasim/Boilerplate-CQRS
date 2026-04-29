using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetSubscriptionStatusCounts;

internal sealed class GetSubscriptionStatusCountsQueryHandler(BillingDbContext billingContext)
    : IRequestHandler<GetSubscriptionStatusCountsQuery, Result<SubscriptionStatusCountsDto>>
{
    public async Task<Result<SubscriptionStatusCountsDto>> Handle(
        GetSubscriptionStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant by intent: this is the platform-admin aggregate.
        var counts = await billingContext.TenantSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = counts.ToDictionary(x => x.Status, x => x.Count);

        var dto = new SubscriptionStatusCountsDto(
            Trialing: dict.GetValueOrDefault(SubscriptionStatus.Trialing),
            Active: dict.GetValueOrDefault(SubscriptionStatus.Active),
            PastDue: dict.GetValueOrDefault(SubscriptionStatus.PastDue),
            Canceled: dict.GetValueOrDefault(SubscriptionStatus.Canceled),
            Expired: dict.GetValueOrDefault(SubscriptionStatus.Expired)
        );

        return Result.Success(dto);
    }
}
