using MediatR;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed record UpdateFeatureFlagCommand(
    Guid Id, string Name, string? Description, string DefaultValue, FlagCategory Category) : IRequest<Result>;
