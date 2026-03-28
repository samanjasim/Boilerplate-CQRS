using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;

internal sealed class CreateFeatureFlagCommandHandler(
    IApplicationDbContext context) : IRequestHandler<CreateFeatureFlagCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var keyExists = await context.FeatureFlags
            .AnyAsync(f => f.Key == request.Key.Trim().ToLowerInvariant(), cancellationToken);
        if (keyExists)
            return Result.Failure<Guid>(FeatureFlagErrors.KeyAlreadyExists);

        var flag = FeatureFlag.Create(request.Key, request.Name, request.Description,
            request.DefaultValue, request.ValueType, request.Category, request.IsSystem);

        context.FeatureFlags.Add(flag);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(flag.Id);
    }
}
