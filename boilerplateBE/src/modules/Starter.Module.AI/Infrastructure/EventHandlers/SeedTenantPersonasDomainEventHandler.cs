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

    public async Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = notification.TenantId;
        var allFactories = AiPersona.AllSeededPersonaFactories;

        var existing = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && allFactories.Keys.Contains(p.Slug))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        var have = existing.ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var (slug, factory) in allFactories)
        {
            if (have.Contains(slug)) continue;
            db.AiPersonas.Add(factory(tenantId, SystemSeedActor));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded {Count} personas for tenant {TenantId} (had {ExistingCount} of {ExpectedCount}).",
                added, tenantId, existing.Count, allFactories.Count);
        }
    }
}
