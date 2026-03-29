using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.OptOutFeatureFlag;

public sealed record OptOutFeatureFlagCommand(Guid FeatureFlagId) : IRequest<Result>;
