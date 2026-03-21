using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Commands.RemoveUserRole;

public sealed record RemoveUserRoleCommand(
    Guid UserId,
    Guid RoleId) : IRequest<Result>;
