using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.ResolveFeatureFlag;

public sealed record ResolveFeatureFlagQuery(string Key) : IRequest<Result<ResolvedFeatureFlagDto>>;
