using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.OptOutFeatureFlag;

internal sealed class OptOutFeatureFlagCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IFeatureFlagService featureFlagService) : IRequestHandler<OptOutFeatureFlagCommand, Result>
{
    public async Task<Result> Handle(OptOutFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (tenantId is null)
            return Result.Failure(Error.Forbidden("Only tenant users can opt out of feature flags."));

        var flag = await context.FeatureFlags.FirstOrDefaultAsync(f => f.Id == request.FeatureFlagId, cancellationToken);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);

        if (flag.IsSystem || flag.ValueType != FlagValueType.Boolean)
            return Result.Failure(FeatureFlagErrors.CannotOptOut);

        var existing = await context.TenantFeatureFlags.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FeatureFlagId == request.FeatureFlagId && t.TenantId == tenantId.Value, cancellationToken);

        if (existing is not null)
        {
            if (existing.Value == "false")
                return Result.Success();

            existing.UpdateValue("false");
        }
        else
        {
            var tenantOverride = TenantFeatureFlag.Create(tenantId.Value, request.FeatureFlagId, "false");
            context.TenantFeatureFlags.Add(tenantOverride);
        }

        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(tenantId.Value, cancellationToken);
        return Result.Success();
    }
}
