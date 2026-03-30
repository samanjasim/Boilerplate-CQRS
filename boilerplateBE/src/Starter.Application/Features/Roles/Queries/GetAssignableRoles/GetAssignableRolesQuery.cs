using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Queries.GetAssignableRoles;

/// <summary>
/// Returns roles the current user can assign to others.
/// Filtered by permission hierarchy (target role perms must be subset of caller's)
/// and tenant scope.
/// </summary>
public sealed record GetAssignableRolesQuery(
    Guid? TenantId = null) : IRequest<Result<IReadOnlyList<RoleDto>>>;
