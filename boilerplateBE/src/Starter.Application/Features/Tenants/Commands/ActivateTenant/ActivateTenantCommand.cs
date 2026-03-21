using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.ActivateTenant;

public sealed record ActivateTenantCommand(Guid Id) : IRequest<Result>;
