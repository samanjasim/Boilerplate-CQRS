using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveOptOut;

internal sealed class RemoveOptOutCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IFeatureFlagService featureFlagService) : IRequestHandler<RemoveOptOutCommand, Result>
{
    public async Task<Result> Handle(RemoveOptOutCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (tenantId is null)
            return Result.Failure(Error.Forbidden("Only tenant users can manage opt-out."));

        var existing = await context.Set<TenantFeatureFlag>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.FeatureFlagId == request.FeatureFlagId && t.TenantId == tenantId.Value, cancellationToken);

        if (existing is not null)
        {
            context.Set<TenantFeatureFlag>().Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            await featureFlagService.InvalidateCacheAsync(tenantId.Value, cancellationToken);
        }

        return Result.Success();
    }
}
