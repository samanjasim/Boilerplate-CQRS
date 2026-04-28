using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

/// <summary>
/// Plan 5e: idempotently adds the six flagship demo personas (student, teacher,
/// parent, editor, approver, client) to every tenant that doesn't yet have them.
/// New tenants get these via <c>SeedTenantPersonasDomainEventHandler</c>; this seed
/// covers tenants that pre-date 5e.
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

        foreach (var tenantId in tenantIds)
        {
            var existing = await db.AiPersonas
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.Slug)
                .ToListAsync(ct);
            var have = existing.ToHashSet(StringComparer.Ordinal);

            if (!have.Contains(AiPersona.StudentSlug)) db.AiPersonas.Add(AiPersona.CreateStudent(tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.TeacherSlug)) db.AiPersonas.Add(AiPersona.CreateTeacher(tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.ParentSlug)) db.AiPersonas.Add(AiPersona.CreateParent(tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.EditorSlug)) db.AiPersonas.Add(AiPersona.CreateEditor(tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.ApproverSlug)) db.AiPersonas.Add(AiPersona.CreateApprover(tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.ClientSlug)) db.AiPersonas.Add(AiPersona.CreateClient(tenantId, SystemSeedActor));

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(ct);
        }
    }
}
