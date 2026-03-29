using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;

public sealed record RemoveTenantOverrideCommand(Guid FeatureFlagId, Guid TenantId) : IRequest<Result>;
