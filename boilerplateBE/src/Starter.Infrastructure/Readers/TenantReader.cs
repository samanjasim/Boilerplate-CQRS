using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Readers;

/// <summary>
/// Default <see cref="ITenantReader"/> implementation backed by
/// <see cref="IApplicationDbContext"/>. Modules with their own DbContext
/// inject this reader instead of joining across contexts.
/// </summary>
public sealed class TenantReader(IApplicationDbContext db) : ITenantReader
{
    public async Task<TenantSummary?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantSummary(t.Id, t.Name, t.Slug, t.Status.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenantSummary>> GetManyAsync(
        IEnumerable<Guid> tenantIds,
        CancellationToken cancellationToken = default)
    {
        var ids = tenantIds.ToList();
        if (ids.Count == 0) return [];

        return await db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new TenantSummary(t.Id, t.Name, t.Slug, t.Status.Name))
            .ToListAsync(cancellationToken);
    }
}
