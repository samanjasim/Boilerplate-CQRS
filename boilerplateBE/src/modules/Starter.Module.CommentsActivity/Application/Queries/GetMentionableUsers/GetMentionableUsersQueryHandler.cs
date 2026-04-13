using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetMentionableUsers;

internal sealed class GetMentionableUsersQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<GetMentionableUsersQuery, Result<List<MentionableUserDto>>>
{
    public async Task<Result<List<MentionableUserDto>>> Handle(
        GetMentionableUsersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().AsQueryable();

        // Tenant users can only mention users in their own tenant
        if (currentUser.TenantId.HasValue)
        {
            query = query.Where(u => u.TenantId == currentUser.TenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.FullName.FirstName.ToLower().Contains(term) ||
                u.FullName.LastName.ToLower().Contains(term) ||
                u.Email.Value.ToLower().Contains(term));
        }

        var users = await query
            .OrderBy(u => u.Username)
            .Take(request.PageSize)
            .Select(u => new MentionableUserDto(
                u.Id,
                u.Username,
                u.FullName.FirstName + " " + u.FullName.LastName,
                u.Email.Value))
            .ToListAsync(cancellationToken);

        return Result.Success(users);
    }
}
