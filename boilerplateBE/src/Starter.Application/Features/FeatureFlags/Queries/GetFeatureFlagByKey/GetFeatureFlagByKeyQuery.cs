using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;

public sealed record GetFeatureFlagByKeyQuery(string Key) : IRequest<Result<FeatureFlagDto>>;
