using Starter.Application.Common.Models;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : PaginationQuery, IRequest<Result<PaginatedList<RoleDto>>>;
