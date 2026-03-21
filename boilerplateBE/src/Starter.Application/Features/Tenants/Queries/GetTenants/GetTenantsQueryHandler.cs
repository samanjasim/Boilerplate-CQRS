using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Tenants.DTOs;
using Starter.Domain.Tenants.Enums;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Queries.GetTenants;

internal sealed class GetTenantsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetTenantsQuery, Result<PaginatedList<TenantDto>>>
{
    public async Task<Result<PaginatedList<TenantDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Tenants
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(searchTerm) ||
                (t.Slug != null && t.Slug.ToLower().Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TenantStatus.FromName(request.Status);
            if (status is not null)
                query = query.Where(t => t.Status == status);
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.SortDescending
                ? query.OrderByDescending(t => t.Name)
                : query.OrderBy(t => t.Name),
            "slug" => request.SortDescending
                ? query.OrderByDescending(t => t.Slug)
                : query.OrderBy(t => t.Slug),
            _ => request.SortDescending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var tenants = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var tenantDtos = tenants.ToDtoList();

        var paginatedList = PaginatedList<TenantDto>.Create(
            tenantDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(paginatedList);
    }
}
