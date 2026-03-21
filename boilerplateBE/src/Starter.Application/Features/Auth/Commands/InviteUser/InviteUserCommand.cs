using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

public sealed record InviteUserCommand(
    string Email,
    Guid RoleId) : IRequest<Result<Guid>>;
