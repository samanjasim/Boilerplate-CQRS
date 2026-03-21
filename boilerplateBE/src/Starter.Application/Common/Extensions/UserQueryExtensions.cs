using Microsoft.EntityFrameworkCore;
using Starter.Domain.Identity.Entities;

namespace Starter.Application.Common.Extensions;

internal static class UserQueryExtensions
{
    public static IQueryable<User> WithRolesAndPermissions(this IQueryable<User> query)
    {
        return query
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission);
    }
}
