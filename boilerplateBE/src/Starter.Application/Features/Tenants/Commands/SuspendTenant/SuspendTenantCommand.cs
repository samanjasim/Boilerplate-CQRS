using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.SuspendTenant;

public sealed record SuspendTenantCommand(Guid Id) : IRequest<Result>;
