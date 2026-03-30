using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;

public sealed record SetTenantDefaultRoleCommand(
    Guid TenantId,
    Guid? RoleId) : IRequest<Result>;
