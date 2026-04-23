using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetMentionableUsers;

internal sealed class GetMentionableUsersQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    ICommentableEntityRegistry entityRegistry,
    IServiceProvider services) : IRequestHandler<GetMentionableUsersQuery, Result<List<MentionableUserDto>>>
{
    public async Task<Result<List<MentionableUserDto>>> Handle(
        GetMentionableUsersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.IgnoreQueryFilters().AsNoTracking().AsQueryable();

        if (currentUser.TenantId.HasValue)
        {
            // Tenant users: only users in their own tenant.
            var tenantId = currentUser.TenantId.Value;
            query = query.Where(u => u.TenantId == tenantId);
        }
        else
        {
            // Platform admin: scope to the entity's tenant + platform users.
            var entityTenantId = await ResolveEntityTenantIdAsync(request, cancellationToken);
            if (entityTenantId.HasValue)
            {
                var tenantId = entityTenantId.Value;
                query = query.Where(u => u.TenantId == tenantId || u.TenantId == null);
            }
            else
            {
                // Global entity (no tenant) — only platform users.
                query = query.Where(u => u.TenantId == null);
            }
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

    private async Task<Guid?> ResolveEntityTenantIdAsync(
        GetMentionableUsersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EntityType) || !request.EntityId.HasValue)
            return null;

        var definition = entityRegistry.GetDefinition(request.EntityType);
        if (definition?.ResolveTenantIdAsync is null)
            return null;

        return await definition.ResolveTenantIdAsync(
            request.EntityId.Value, services, cancellationToken);
    }
}
