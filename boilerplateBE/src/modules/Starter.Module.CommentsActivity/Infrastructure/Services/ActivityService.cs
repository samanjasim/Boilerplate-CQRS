using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Infrastructure.Services;

public sealed class ActivityService(CommentsActivityDbContext context) : IActivityService
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
        var entry = ActivityEntry.Create(
            tenantId, entityType, entityId, action,
            actorId, metadataJson, description);

        context.ActivityEntries.Add(entry);
        await context.SaveChangesAsync(ct);
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
