using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetSubscription;

internal sealed class GetSubscriptionQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetSubscriptionQuery, Result<TenantSubscriptionDto>>
{
    public async Task<Result<TenantSubscriptionDto>> Handle(
        GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;

        if (!tenantId.HasValue)
            return Result.Failure<TenantSubscriptionDto>(BillingErrors.SubscriptionNotFound);

        var subscription = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (subscription is null)
            return Result.Failure<TenantSubscriptionDto>(BillingErrors.SubscriptionNotFound);

        return Result.Success(subscription.ToDto());
    }
}
