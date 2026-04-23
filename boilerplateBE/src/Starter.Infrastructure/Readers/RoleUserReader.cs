using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Enums;

namespace Starter.Infrastructure.Readers;

public sealed class RoleUserReader(IApplicationDbContext db) : IRoleUserReader
{
    public async Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(
        string roleName,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        // Find the role by name within this tenant (or system roles)
        var role = await db.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.Name == roleName
                && (r.TenantId == tenantId || r.IsSystemRole))
            .FirstOrDefaultAsync(ct);

        if (role is null)
            return [];

        // Return all active users assigned that role, scoped to the tenant
        var userIds = await db.UserRoles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ur => ur.RoleId == role.Id)
            .Join(db.Users.AsNoTracking().IgnoreQueryFilters()
                    .Where(u => u.TenantId == tenantId
                        && u.Status == UserStatus.Active),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => u.Id)
            .ToListAsync(ct);

        return userIds;
    }
}
