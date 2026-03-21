using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Commands.UpdateRolePermissions;

public sealed record UpdateRolePermissionsCommand(
    Guid RoleId,
    List<Guid> PermissionIds) : IRequest<Result>;
