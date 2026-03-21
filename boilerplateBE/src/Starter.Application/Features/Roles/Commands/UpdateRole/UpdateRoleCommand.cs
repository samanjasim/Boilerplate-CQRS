using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Commands.UpdateRole;

public sealed record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description) : IRequest<Result>;
