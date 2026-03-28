using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;

internal sealed class RemoveTenantOverrideCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<RemoveTenantOverrideCommand, Result>
{
    public async Task<Result> Handle(RemoveTenantOverrideCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.TenantFeatureFlags.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FeatureFlagId == request.FeatureFlagId && t.TenantId == request.TenantId, cancellationToken);
        if (existing is null) return Result.Failure(FeatureFlagErrors.OverrideNotFound);

        context.TenantFeatureFlags.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(request.TenantId, cancellationToken);
        return Result.Success();
    }
}
