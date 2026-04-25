using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.MarkTenantOnboarded;

/// <summary>
/// Stamp a tenant as having completed (or skipped) the post-registration
/// onboarding wizard. Set <see cref="Onboarded"/> to false to re-trigger
/// the wizard (admins re-running setup).
/// </summary>
public sealed record MarkTenantOnboardedCommand(
    Guid TenantId,
    bool Onboarded) : IRequest<Result>;
