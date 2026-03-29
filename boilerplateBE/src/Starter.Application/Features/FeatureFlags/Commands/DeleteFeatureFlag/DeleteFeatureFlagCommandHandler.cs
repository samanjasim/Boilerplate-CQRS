using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;

internal sealed class DeleteFeatureFlagCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<DeleteFeatureFlagCommand, Result>
{
    public async Task<Result> Handle(DeleteFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var flag = await context.FeatureFlags.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);
        if (flag.IsSystem) return Result.Failure(FeatureFlagErrors.CannotDeleteSystemFlag);

        context.FeatureFlags.Remove(flag);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(cancellationToken: cancellationToken);
        return Result.Success();
    }
}
