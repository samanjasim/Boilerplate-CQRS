using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;

internal sealed class RemoveTenantOverrideCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService,
    ICurrentUserService currentUser) : IRequestHandler<RemoveTenantOverrideCommand, Result>
{
    public async Task<Result> Handle(RemoveTenantOverrideCommand request, CancellationToken cancellationToken)
    {
        // If caller is a tenant user (not platform admin), they can only override their own tenant
        var callerTenantId = currentUser.TenantId;
        if (callerTenantId.HasValue && callerTenantId.Value != request.TenantId)
            return Result.Failure(Error.Forbidden("You can only manage overrides for your own tenant."));

        var existing = await context.TenantFeatureFlags.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FeatureFlagId == request.FeatureFlagId && t.TenantId == request.TenantId, cancellationToken);
        if (existing is null) return Result.Failure(FeatureFlagErrors.OverrideNotFound);

        context.TenantFeatureFlags.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(request.TenantId, cancellationToken);
        return Result.Success();
    }
}
