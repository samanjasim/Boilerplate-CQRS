using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

public static class AiRoleMetadataSeed
{
    // Roles seeded with IsAgentAssignable=false. "Admin" is included because in this
    // boilerplate's seed it IS the tenant admin role (broad tenant-scoped powers). Letting
    // an agent assume Admin would be a privilege-escalation surface for the same reason as
    // SuperAdmin. "TenantAdmin" is kept for forward-compatibility with installations that
    // adopt that naming. Confirmed via 2026-04-27 manual test that Admin was previously
    // assignable to agents.
    private static readonly HashSet<string> LockedRoleNames =
        new(new[] { "SuperAdmin", "TenantAdmin", "Admin" }, StringComparer.OrdinalIgnoreCase);

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
