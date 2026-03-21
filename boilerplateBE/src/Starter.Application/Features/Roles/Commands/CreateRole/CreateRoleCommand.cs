using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Commands.CreateRole;

public sealed record CreateRoleCommand(
    string Name,
    string? Description) : IRequest<Result<Guid>>;
