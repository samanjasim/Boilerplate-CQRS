using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

public sealed record InviteUserCommand(
    string Email,
    Guid? RoleId = null,
    Guid? TenantId = null) : IRequest<Result<Guid>>;
