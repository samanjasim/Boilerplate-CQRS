using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;

internal sealed class SetTenantOverrideCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService,
    ICurrentUserService currentUser) : IRequestHandler<SetTenantOverrideCommand, Result>
{
    public async Task<Result> Handle(SetTenantOverrideCommand request, CancellationToken cancellationToken)
    {
        var flagExists = await context.Set<FeatureFlag>().AnyAsync(f => f.Id == request.FeatureFlagId, cancellationToken);
        if (!flagExists) return Result.Failure(FeatureFlagErrors.NotFound);

        // If caller is a tenant user (not platform admin), they can only override their own tenant
        var callerTenantId = currentUser.TenantId;
        if (callerTenantId.HasValue && callerTenantId.Value != request.TenantId)
            return Result.Failure(Error.Forbidden("You can only manage overrides for your own tenant."));

        var existing = await context.Set<TenantFeatureFlag>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FeatureFlagId == request.FeatureFlagId && t.TenantId == request.TenantId, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateValue(request.Value, OverrideSource.Manual);
        }
        else
        {
            var tenantOverride = TenantFeatureFlag.Create(request.TenantId, request.FeatureFlagId, request.Value);
            context.Set<TenantFeatureFlag>().Add(tenantOverride);
        }

        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(request.TenantId, cancellationToken);
        return Result.Success();
    }
}
