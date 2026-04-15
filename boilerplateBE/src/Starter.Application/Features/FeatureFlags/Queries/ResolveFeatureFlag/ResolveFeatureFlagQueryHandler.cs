using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.ResolveFeatureFlag;

internal sealed class ResolveFeatureFlagQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<ResolveFeatureFlagQuery, Result<ResolvedFeatureFlagDto>>
{
    public async Task<Result<ResolvedFeatureFlagDto>> Handle(
        ResolveFeatureFlagQuery request, CancellationToken cancellationToken)
    {
        var key = request.Key.Trim().ToLowerInvariant();

        var flag = await context.Set<FeatureFlag>().AsNoTracking()
            .Where(f => f.Key == key)
            .Select(f => new { f.Id, f.DefaultValue })
            .FirstOrDefaultAsync(cancellationToken);

        if (flag is null)
            return Result.Failure<ResolvedFeatureFlagDto>(FeatureFlagErrors.NotFound);

        string? overrideValue = null;
        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            overrideValue = await context.Set<TenantFeatureFlag>().AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value && t.FeatureFlagId == flag.Id)
                .Select(t => t.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result.Success(new ResolvedFeatureFlagDto(key, overrideValue ?? flag.DefaultValue));
    }
}
