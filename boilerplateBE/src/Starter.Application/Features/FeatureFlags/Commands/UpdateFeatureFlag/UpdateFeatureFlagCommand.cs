using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed record UpdateFeatureFlagCommand(
    Guid Id, string Name, string? Description, string DefaultValue, string? Category) : IRequest<Result>;
