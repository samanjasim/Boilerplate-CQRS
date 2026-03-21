using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.DeactivateTenant;

public sealed record DeactivateTenantCommand(Guid Id) : IRequest<Result>;
