using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using AbsWatchReason = Starter.Abstractions.Capabilities.WatchReason;

namespace Starter.Module.CommentsActivity.Infrastructure.Services;

public sealed class EntityWatcherService(
    CommentsActivityDbContext context,
    ICommentableEntityRegistry registry,
    IServiceProvider services,
    ILogger<EntityWatcherService> logger) : IEntityWatcherService
{
    public async Task WatchAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        Guid userId,
        AbsWatchReason reason = AbsWatchReason.Explicit,
        CancellationToken ct = default)
    {
        var effectiveTenantId = await TenantResolution.ResolveEffectiveTenantIdAsync(
            registry, services, logger, entityType, entityId, tenantId, ct);

        // Dedup must be cross-tenant: the unique index is (entity_type,
        // entity_id, user_id) with no tenant column, so a stale row from a
        // prior cross-tenant action (tenant_id = NULL) would collide with a
        // new insert under the active tenant if we relied on the tenant
        // filter alone.
        var alreadyWatching = await context.EntityWatchers
            .IgnoreQueryFilters()
            .AnyAsync(
                w => w.EntityType == entityType &&
                     w.EntityId == entityId &&
                     w.UserId == userId,
                ct);

        if (alreadyWatching) return;

        var domainReason = reason switch
        {
            AbsWatchReason.Participated => Domain.Enums.WatchReason.Participated,
            AbsWatchReason.Mentioned => Domain.Enums.WatchReason.Mentioned,
            AbsWatchReason.Created => Domain.Enums.WatchReason.Created,
            _ => Domain.Enums.WatchReason.Explicit,
        };

        var watcher = EntityWatcher.Create(effectiveTenantId, entityType, entityId, userId, domainReason);
        context.EntityWatchers.Add(watcher);
        await context.SaveChangesAsync(ct);
    }

    public async Task UnwatchAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct = default)
    {
        var watcher = await context.EntityWatchers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                w => w.EntityType == entityType &&
                     w.EntityId == entityId &&
                     w.UserId == userId,
                ct);

        if (watcher is null) return;

        var resolved = await TenantResolution.ResolveEffectiveTenantIdAsync(
            registry, services, logger, watcher.EntityType, watcher.EntityId, watcher.TenantId, ct);

        if (resolved != watcher.TenantId) return;

        context.EntityWatchers.Remove(watcher);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> IsWatchingAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.EntityWatchers
            .AnyAsync(
                w => w.EntityType == entityType &&
                     w.EntityId == entityId &&
                     w.UserId == userId,
                ct);
    }

    public async Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        return await context.EntityWatchers
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .Select(w => w.UserId)
            .ToListAsync(ct);
    }
}
