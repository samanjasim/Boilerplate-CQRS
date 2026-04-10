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

        var flag = await context.Set<FeatureFlag>().FirstOrDefaultAsync(f => f.Id == request.FeatureFlagId, cancellationToken);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);

        if (flag.IsSystem || flag.ValueType != FlagValueType.Boolean)
            return Result.Failure(FeatureFlagErrors.CannotOptOut);

        // Resolve the current value — only allow opt-out if the flag is currently ENABLED
        var resolvedValue = await featureFlagService.GetValueAsync<string>(flag.Key, cancellationToken);
        if (resolvedValue != "true" && resolvedValue != "True")
            return Result.Failure(Error.Validation("FeatureFlags.OptOut", "Cannot opt out of a feature that is not enabled for your tenant."));

        var existing = await context.Set<TenantFeatureFlag>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FeatureFlagId == request.FeatureFlagId && t.TenantId == tenantId.Value, cancellationToken);

        if (existing is not null)
        {
            // Don't allow overwriting plan-based overrides with manual opt-out
            if (existing.Source == OverrideSource.PlanSubscription)
                return Result.Failure(Error.Validation("FeatureFlags.OptOut", "Cannot opt out of a feature controlled by your subscription plan."));

            if (existing.Value == "false")
                return Result.Success();

            existing.UpdateValue("false", OverrideSource.Manual);
        }
        else
        {
            var tenantOverride = TenantFeatureFlag.Create(tenantId.Value, request.FeatureFlagId, "false");
            context.Set<TenantFeatureFlag>().Add(tenantOverride);
        }

        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(tenantId.Value, cancellationToken);
        return Result.Success();
    }
}
