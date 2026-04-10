using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;

internal sealed class GetFeatureFlagByKeyQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetFeatureFlagByKeyQuery, Result<FeatureFlagDto>>
{
    public async Task<Result<FeatureFlagDto>> Handle(
        GetFeatureFlagByKeyQuery request, CancellationToken cancellationToken)
    {
        var flag = await context.Set<FeatureFlag>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == request.Key.Trim().ToLowerInvariant(), cancellationToken);
        if (flag is null)
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.NotFound);

        string? overrideValue = null;
        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            overrideValue = await context.Set<TenantFeatureFlag>().AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value && t.FeatureFlagId == flag.Id)
                .Select(t => t.Value).FirstOrDefaultAsync(cancellationToken);
        }

        return Result.Success(flag.ToDto(overrideValue));
    }
}
