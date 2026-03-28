using MediatR;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;

public sealed record CreateFeatureFlagCommand(
    string Key, string Name, string? Description, string DefaultValue,
    FlagValueType ValueType, string? Category, bool IsSystem) : IRequest<Result<Guid>>;
