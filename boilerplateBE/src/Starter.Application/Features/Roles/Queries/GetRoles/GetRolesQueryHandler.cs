using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Queries.GetRoles;

internal sealed class GetRolesQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetRolesQuery, Result<PaginatedList<RoleDto>>>
{
    public async Task<Result<PaginatedList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(searchTerm) ||
                (r.Description != null && r.Description.ToLower().Contains(searchTerm)));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.SortDescending
                ? query.OrderByDescending(r => r.Name)
                : query.OrderBy(r => r.Name),
            "createdat" => request.SortDescending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt),
            _ => query.OrderBy(r => r.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var roles = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var roleDtos = roles.ToDtoList();

        var paginatedList = PaginatedList<RoleDto>.Create(
            roleDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(paginatedList);
    }
}
