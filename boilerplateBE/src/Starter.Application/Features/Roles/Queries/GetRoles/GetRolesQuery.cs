using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : PaginationQuery, IRequest<Result<PaginatedList<RoleDto>>>
{
    /// <summary>
    /// Optional tenant filter. Platform admin can pass a tenant ID to see that tenant's custom roles.
    /// </summary>
    public Guid? TenantId { get; init; }
}
