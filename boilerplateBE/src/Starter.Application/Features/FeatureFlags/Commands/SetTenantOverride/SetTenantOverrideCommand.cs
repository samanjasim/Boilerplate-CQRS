using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;

public sealed record SetTenantOverrideCommand(Guid FeatureFlagId, Guid TenantId, string Value) : IRequest<Result>;
