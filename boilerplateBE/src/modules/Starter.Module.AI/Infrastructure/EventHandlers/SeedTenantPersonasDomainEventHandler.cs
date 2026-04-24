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

        var existing = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId &&
                        (p.Slug == AiPersona.AnonymousSlug || p.Slug == AiPersona.DefaultSlug))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);

        var hasAnonymous = existing.Contains(AiPersona.AnonymousSlug);
        var hasDefault = existing.Contains(AiPersona.DefaultSlug);

        if (!hasAnonymous)
            db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantId, SystemSeedActor));
        if (!hasDefault)
            db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, SystemSeedActor));

        if (!hasAnonymous || !hasDefault)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded personas for tenant {TenantId} (anonymous={Anon}, default={Default}).",
                tenantId, !hasAnonymous, !hasDefault);
        }
    }
}
