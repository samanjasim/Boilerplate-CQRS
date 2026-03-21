using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id) : IRequest<Result>;
