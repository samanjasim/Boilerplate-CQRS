namespace Starter.Abstractions.Events.CommentsActivity;

/// <summary>
/// Published when an activity-log entry is recorded against any entity.
/// Consumers register an <c>IConsumer&lt;ActivityRecordedIntegrationEvent&gt;</c>
/// in their own module assembly; MassTransit's <c>AddConsumers(assembly)</c>
/// auto-wires them. <c>MetadataJson</c> is opaque — whatever the caller passed
/// to <c>IActivityService.RecordAsync</c>.
/// </summary>
public sealed record ActivityRecordedIntegrationEvent(
    Guid ActivityEntryId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    string Action,
    Guid? ActorId,
    string? MetadataJson,
    string? Description,
    DateTime OccurredAt);
