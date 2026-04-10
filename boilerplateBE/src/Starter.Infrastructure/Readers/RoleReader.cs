using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Readers;

public sealed class RoleReader(IApplicationDbContext db) : IRoleReader
{
    public async Task<RoleSummary?> GetAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await db.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.Id == roleId)
            .Select(r => new RoleSummary(r.Id, r.Name, r.TenantId, r.IsSystemRole))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleSummary>> GetManyAsync(
        IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = roleIds.ToList();
        if (ids.Count == 0) return [];

        return await db.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => ids.Contains(r.Id))
            .Select(r => new RoleSummary(r.Id, r.Name, r.TenantId, r.IsSystemRole))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleSummary?> GetByNameAsync(
        string name,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await db.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.Name == name && r.TenantId == tenantId)
            .Select(r => new RoleSummary(r.Id, r.Name, r.TenantId, r.IsSystemRole))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
