using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

/// <summary>
/// Plan 5e: idempotently adds the six flagship demo personas (student, teacher,
/// parent, editor, approver, client) to every tenant that doesn't yet have them.
/// New tenants get these via <see cref="EventHandlers.SeedTenantPersonasDomainEventHandler"/>;
/// this seed covers tenants that pre-date 5e.
/// </summary>
internal static class FlagshipPersonasBackfillSeed
{
    private static readonly Guid SystemSeedActor = Guid.Empty;

    public static async Task SeedAsync(
        AiDbContext db,
        IApplicationDbContext appDb,
        CancellationToken ct = default)
    {
        var tenantIds = await appDb.Tenants
            .IgnoreQueryFilters()
            .Select(t => t.Id)
            .ToListAsync(ct);
        if (tenantIds.Count == 0) return;

        var flagshipFactories = AiPersona.FlagshipDemoPersonaFactories;
        var flagshipSlugs = flagshipFactories.Keys.ToHashSet(StringComparer.Ordinal);

        // Single bulk read for every flagship persona row across all tenants.
        var existingByTenant = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => tenantIds.Contains(p.TenantId!.Value) && flagshipSlugs.Contains(p.Slug))
            .Select(p => new { p.TenantId, p.Slug })
            .ToListAsync(ct);

        var haveByTenant = existingByTenant
            .GroupBy(x => x.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Slug).ToHashSet(StringComparer.Ordinal));

        foreach (var tenantId in tenantIds)
        {
            haveByTenant.TryGetValue(tenantId, out var have);
            have ??= new HashSet<string>(StringComparer.Ordinal);

            foreach (var (slug, factory) in flagshipFactories)
            {
                if (have.Contains(slug)) continue;
                db.AiPersonas.Add(factory(tenantId, SystemSeedActor));
            }
        }

        // Single SaveChangesAsync covers every tenant's missing rows.
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }
}
