using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Auth.DTOs;
using Starter.Application.Features.Users.DTOs;
using Starter.Domain.Identity.Enums;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetUsersQuery, Result<PaginatedList<UserDto>>>
{
    public async Task<Result<PaginatedList<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(searchTerm) ||
                u.Email.Value.ToLower().Contains(searchTerm) ||
                u.FullName.FirstName.ToLower().Contains(searchTerm) ||
                u.FullName.LastName.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = UserStatus.FromName(request.Status);
            if (status is not null)
                query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleName = request.Role.Trim();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == roleName));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "username" => request.SortDescending
                ? query.OrderByDescending(u => u.Username)
                : query.OrderBy(u => u.Username),
            "email" => request.SortDescending
                ? query.OrderByDescending(u => u.Email.Value)
                : query.OrderBy(u => u.Email.Value),
            "firstname" => request.SortDescending
                ? query.OrderByDescending(u => u.FullName.FirstName)
                : query.OrderBy(u => u.FullName.FirstName),
            _ => request.SortDescending
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userDtos = users.ToDtoList();

        var paginatedList = PaginatedList<UserDto>.Create(
            userDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(paginatedList);
    }
}
