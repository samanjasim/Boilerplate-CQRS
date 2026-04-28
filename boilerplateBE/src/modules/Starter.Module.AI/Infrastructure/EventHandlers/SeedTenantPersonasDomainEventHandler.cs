using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Domain.Tenants.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.EventHandlers;

internal sealed class SeedTenantPersonasDomainEventHandler(
    AiDbContext db,
    ILogger<SeedTenantPersonasDomainEventHandler> logger)
    : INotificationHandler<TenantCreatedEvent>
{
    private static readonly Guid SystemSeedActor = Guid.Empty;

    private static readonly IReadOnlyList<string> AllSlugs = new[]
    {
        AiPersona.AnonymousSlug, AiPersona.DefaultSlug,
        AiPersona.StudentSlug, AiPersona.TeacherSlug, AiPersona.ParentSlug,
        AiPersona.EditorSlug, AiPersona.ApproverSlug, AiPersona.ClientSlug,
    };

    public async Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = notification.TenantId;

        var existing = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && AllSlugs.Contains(p.Slug))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        var have = existing.ToHashSet(StringComparer.Ordinal);

        var added = 0;
        if (!have.Contains(AiPersona.AnonymousSlug)) { db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.DefaultSlug)) { db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.StudentSlug)) { db.AiPersonas.Add(AiPersona.CreateStudent(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.TeacherSlug)) { db.AiPersonas.Add(AiPersona.CreateTeacher(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.ParentSlug)) { db.AiPersonas.Add(AiPersona.CreateParent(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.EditorSlug)) { db.AiPersonas.Add(AiPersona.CreateEditor(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.ApproverSlug)) { db.AiPersonas.Add(AiPersona.CreateApprover(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.ClientSlug)) { db.AiPersonas.Add(AiPersona.CreateClient(tenantId, SystemSeedActor)); added++; }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded {Count} personas for tenant {TenantId} (had {ExistingCount} of 8).",
                added, tenantId, existing.Count);
        }
    }
}
