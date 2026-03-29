using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveOptOut;

public sealed record RemoveOptOutCommand(Guid FeatureFlagId) : IRequest<Result>;
