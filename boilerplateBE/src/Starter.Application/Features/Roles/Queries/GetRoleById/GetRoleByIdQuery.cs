using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid Id) : IRequest<Result<RoleDto>>;
