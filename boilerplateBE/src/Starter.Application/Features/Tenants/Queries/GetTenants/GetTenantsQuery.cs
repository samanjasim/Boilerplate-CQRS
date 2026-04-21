using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.Tenants.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Queries.GetTenants;

public sealed record GetTenantsQuery : PaginationQuery, IRequest<Result<PaginatedList<TenantDto>>>
{
    public string? Status { get; init; }
}
