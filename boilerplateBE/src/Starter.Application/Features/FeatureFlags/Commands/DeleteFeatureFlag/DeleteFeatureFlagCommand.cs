using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;

public sealed record DeleteFeatureFlagCommand(Guid Id) : IRequest<Result>;
