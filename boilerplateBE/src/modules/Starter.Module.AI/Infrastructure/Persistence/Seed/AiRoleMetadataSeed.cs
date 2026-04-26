using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

public static class AiRoleMetadataSeed
{
    public static async Task SeedAsync(AiDbContext aiDb, IApplicationDbContext appDb, CancellationToken ct = default)
    {
        // Look up SuperAdmin and TenantAdmin role IDs in core
        var lockedRoleNames = new[] { "SuperAdmin", "TenantAdmin" };
        var lockedRoles = await appDb.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => lockedRoleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var roleId in lockedRoles)
        {
            var existing = await aiDb.AiRoleMetadataEntries.FirstOrDefaultAsync(m => m.RoleId == roleId, ct);
            if (existing is null)
            {
                aiDb.AiRoleMetadataEntries.Add(AiRoleMetadata.Create(roleId, isAgentAssignable: false));
            }
            else if (existing.IsAgentAssignable)
            {
                existing.SetAgentAssignable(false);
            }
        }
        await aiDb.SaveChangesAsync(ct);
    }
}
