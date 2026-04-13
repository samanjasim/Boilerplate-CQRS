namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Lightweight projection of an activity log entry, returned by
/// <see cref="IActivityService"/> so consumers never depend on the domain
/// entity directly.
/// </summary>
public sealed record ActivitySummary(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ActorId,
    string? MetadataJson,
    string? Description,
    DateTime CreatedAt);
