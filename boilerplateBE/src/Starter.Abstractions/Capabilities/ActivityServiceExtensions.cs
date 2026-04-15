using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Syntactic sugar over <see cref="IActivityService.RecordAsync"/>. The
/// interface stays stringly-typed for flexibility; this overload removes the
/// repeated <c>JsonSerializer.Serialize(new {...})</c> boilerplate at call
/// sites that have a real metadata shape.
/// </summary>
public static class ActivityServiceExtensions
{
    private static readonly JsonSerializerOptions MetadataJsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Record an activity entry, serializing <paramref name="metadata"/> to
    /// JSON with <c>JsonSerializerDefaults.Web</c>. Equivalent to calling
    /// <see cref="IActivityService.RecordAsync"/> with a hand-serialized
    /// <c>metadataJson</c>; reads still return the raw JSON string.
    /// </summary>
    public static Task RecordAsync<TMetadata>(
        this IActivityService service,
        string entityType,
        Guid entityId,
        Guid? tenantId,
        string action,
        Guid? actorId,
        TMetadata metadata,
        string? description = null,
        CancellationToken ct = default) where TMetadata : class
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(metadata);

        var json = JsonSerializer.Serialize(metadata, MetadataJsonOptions);
        return service.RecordAsync(
            entityType, entityId, tenantId, action, actorId,
            json, description, ct);
    }
}
