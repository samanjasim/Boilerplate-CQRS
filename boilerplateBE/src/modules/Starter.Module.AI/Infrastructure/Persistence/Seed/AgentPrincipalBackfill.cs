using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

/// <summary>
/// One-time backfill for environments that had assistants before Plan 5d-1 introduced
/// the `AiAgentPrincipal` pairing. Idempotent: scans for assistants without a principal
/// and inserts one. Safe to leave wired in indefinitely — once steady-state, finds nothing.
/// </summary>
public static class AgentPrincipalBackfill
{
    public static async Task RunAsync(AiDbContext db, CancellationToken ct = default)
    {
        var unpaired = await db.AiAssistants
            .IgnoreQueryFilters()
            .Where(a => a.TenantId != null
                && !db.AiAgentPrincipals.IgnoreQueryFilters().Any(p => p.AiAssistantId == a.Id))
            .Select(a => new { a.Id, a.TenantId, a.IsActive })
            .ToListAsync(ct);

        if (unpaired.Count == 0) return;

        foreach (var a in unpaired)
        {
            db.AiAgentPrincipals.Add(AiAgentPrincipal.Create(a.Id, a.TenantId!.Value, a.IsActive));
        }
        await db.SaveChangesAsync(ct);
    }
}
