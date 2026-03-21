using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Commands.AssignUserRole;

public sealed record AssignUserRoleCommand(
    Guid UserId,
    Guid RoleId) : IRequest<Result>;
