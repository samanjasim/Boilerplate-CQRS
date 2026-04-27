using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

public static class AiRoleMetadataSeed
{
    private static readonly HashSet<string> LockedRoleNames =
        new(new[] { "SuperAdmin", "TenantAdmin" }, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Seeds an `AiRoleMetadata` row for every core role so the assignment validator
    /// (`AssignAgentRoleCommandHandler`) can fail-closed when no row is present.
    /// SuperAdmin and TenantAdmin are seeded `IsAgentAssignable=false`; all others
    /// default to `true`. Idempotent: existing rows are reconciled to the expected
    /// value if locked-role membership has changed since the previous seed.
    /// </summary>
    public static async Task SeedAsync(AiDbContext aiDb, IApplicationDbContext appDb, CancellationToken ct = default)
    {
        var allRoles = await appDb.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);

        if (allRoles.Count == 0) return;

        var existingByRoleId = await aiDb.AiRoleMetadataEntries
            .ToDictionaryAsync(m => m.RoleId, ct);

        foreach (var role in allRoles)
        {
            var shouldBeAssignable = !LockedRoleNames.Contains(role.Name);

            if (!existingByRoleId.TryGetValue(role.Id, out var existing))
            {
                aiDb.AiRoleMetadataEntries.Add(AiRoleMetadata.Create(role.Id, shouldBeAssignable));
            }
            else if (existing.IsAgentAssignable != shouldBeAssignable)
            {
                existing.SetAgentAssignable(shouldBeAssignable);
            }
        }

        await aiDb.SaveChangesAsync(ct);
    }
}
