using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

internal sealed class UpdateFeatureFlagCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<UpdateFeatureFlagCommand, Result>
{
    public async Task<Result> Handle(UpdateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var flag = await context.Set<FeatureFlag>().FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);

        flag.Update(request.Name, request.Description, request.DefaultValue, request.Category);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(cancellationToken: cancellationToken);
        return Result.Success();
    }
}
