using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Domain.Identity.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.EventHandlers;

internal sealed class AssignDefaultPersonaDomainEventHandler(
    AiDbContext db,
    ILogger<AssignDefaultPersonaDomainEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TenantId is not Guid tenantId)
        {
            logger.LogDebug("UserCreated for {UserId} has no tenant; skipping persona assignment.",
                notification.UserId);
            return;
        }

        var alreadyAssigned = await db.UserPersonas
            .IgnoreQueryFilters()
            .AnyAsync(up => up.UserId == notification.UserId, cancellationToken);
        if (alreadyAssigned)
            return;

        var @default = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.Slug == AiPersona.DefaultSlug && p.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        var persona = @default ?? await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId &&
                        p.AudienceType == PersonaAudienceType.Internal &&
                        p.IsActive)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (persona is null)
        {
            logger.LogWarning(
                "No eligible persona found for tenant {TenantId}; user {UserId} left unassigned.",
                tenantId, notification.UserId);
            return;
        }

        db.UserPersonas.Add(UserPersona.Create(
            userId: notification.UserId,
            personaId: persona.Id,
            tenantId: tenantId,
            isDefault: true,
            assignedBy: null));

        await db.SaveChangesAsync(cancellationToken);
    }
}
