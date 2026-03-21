using System.Text.Json;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Starter.Infrastructure.Persistence.Interceptors;

public sealed class AuditableEntityInterceptor(
    IDateTimeService dateTimeService,
    ICurrentUserService currentUserService,
    IAuditContextProvider auditContextProvider) : SaveChangesInterceptor
{
    private static readonly Dictionary<string, AuditEntityType> EntityTypeMap = new()
    {
        ["User"] = AuditEntityType.User,
        ["Role"] = AuditEntityType.Role,
        ["Permission"] = AuditEntityType.Permission,
        ["Tenant"] = AuditEntityType.Tenant,
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        CreateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        CreateAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null) return;

        var utcNow = dateTimeService.UtcNow;
        var userId = currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                SetProperty(entry, nameof(BaseEntity.CreatedAt), utcNow);
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                SetProperty(entry, nameof(BaseEntity.ModifiedAt), utcNow);
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                SetProperty(entry, nameof(BaseAuditableEntity.CreatedBy), userId);
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                SetProperty(entry, nameof(BaseAuditableEntity.ModifiedBy), userId);
            }
        }
    }

    private void CreateAuditLogs(DbContext? context)
    {
        if (context is null) return;

        var utcNow = dateTimeService.UtcNow;
        var userId = currentUserService.UserId;
        var ipAddress = auditContextProvider.IpAddress;
        var correlationId = auditContextProvider.CorrelationId;
        var performedByName = auditContextProvider.UserDisplayName;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var entityTypeName = entry.Entity.GetType().Name;

            if (!EntityTypeMap.TryGetValue(entityTypeName, out var auditEntityType))
                continue;

            var entityId = GetEntityId(entry);
            if (entityId is null)
                continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Modified => AuditAction.Updated,
                EntityState.Deleted => AuditAction.Deleted,
                _ => (AuditAction?)null
            };

            if (action is null)
                continue;

            var changes = CaptureChanges(entry);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = auditEntityType,
                EntityId = entityId.Value,
                Action = action.Value,
                Changes = changes,
                PerformedBy = userId,
                PerformedByName = performedByName,
                PerformedAt = utcNow,
                IpAddress = ipAddress,
                CorrelationId = correlationId
            };

            context.Set<AuditLog>().Add(auditLog);
        }
    }

    private static Guid? GetEntityId(EntityEntry entry)
    {
        var idProperty = entry.Property("Id");
        if (idProperty.CurrentValue is Guid guidId)
            return guidId;

        return null;
    }

    private static string? CaptureChanges(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        switch (entry.State)
        {
            case EntityState.Added:
                foreach (var property in entry.Properties)
                {
                    if (ShouldSkipProperty(property.Metadata.Name))
                        continue;
                    newValues[property.Metadata.Name] = property.CurrentValue;
                }
                break;

            case EntityState.Modified:
                foreach (var property in entry.Properties)
                {
                    if (ShouldSkipProperty(property.Metadata.Name))
                        continue;
                    if (!property.IsModified)
                        continue;

                    oldValues[property.Metadata.Name] = property.OriginalValue;
                    newValues[property.Metadata.Name] = property.CurrentValue;
                }
                break;

            case EntityState.Deleted:
                foreach (var property in entry.Properties)
                {
                    if (ShouldSkipProperty(property.Metadata.Name))
                        continue;
                    oldValues[property.Metadata.Name] = property.OriginalValue;
                }
                break;
        }

        if (oldValues.Count == 0 && newValues.Count == 0)
            return null;

        var changes = new { OldValues = oldValues, NewValues = newValues };
        return JsonSerializer.Serialize(changes);
    }

    private static bool ShouldSkipProperty(string propertyName)
    {
        return propertyName is "PasswordHash" or "RefreshToken" or "RefreshTokenExpiresAt";
    }

    private static void SetProperty<T>(EntityEntry entry, string propertyName, T value)
    {
        var property = entry.Property(propertyName);
        property.CurrentValue = value;
    }
}
