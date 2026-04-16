using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Infrastructure.Services;

public sealed class ActivityService(
    CommentsActivityDbContext context,
    ICommentableEntityRegistry registry,
    IServiceProvider services,
    IMessagePublisher messagePublisher,
    TimeProvider clock,
    ILogger<ActivityService> logger) : IActivityService
{
    public async Task RecordAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        string action,
        Guid? actorId,
        string? metadataJson = null,
        string? description = null,
        CancellationToken ct = default)
    {
        var effectiveTenantId = await TenantResolution.ResolveEffectiveTenantIdAsync(
            registry, services, logger, entityType, entityId, tenantId, ct);

        var entry = ActivityEntry.Create(
            effectiveTenantId, entityType, entityId, action,
            actorId, metadataJson, description);

        context.ActivityEntries.Add(entry);
        await context.SaveChangesAsync(ct);

        // At-most-once publish: CommentsActivityDbContext is not bound to the
        // MassTransit outbox (ApplicationDbContext is). A crash between the
        // save and the publish below drops the integration event. Acceptable
        // today because internal notifications already cover the UX surface;
        // when a consumer requires delivery guarantees, see ROADMAP.md —
        // "Transactional outbox on CommentsActivityDbContext".
        await messagePublisher.PublishAsync(
            new ActivityRecordedIntegrationEvent(
                entry.Id,
                entry.EntityType,
                entry.EntityId,
                entry.TenantId,
                entry.Action,
                entry.ActorId,
                entry.MetadataJson,
                entry.Description,
                clock.GetUtcNow().UtcDateTime),
            ct);
    }

    public async Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(
        string entityType,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return await context.ActivityEntries
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivitySummary(
                a.Id, a.EntityType, a.EntityId, a.Action,
                a.ActorId, a.MetadataJson, a.Description, a.CreatedAt))
            .ToListAsync(ct);
    }
}
