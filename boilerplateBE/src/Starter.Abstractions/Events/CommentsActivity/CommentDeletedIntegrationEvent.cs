namespace Starter.Abstractions.Events.CommentsActivity;

/// <summary>
/// Published when a comment is deleted. Consumers register an
/// <c>IConsumer&lt;CommentDeletedIntegrationEvent&gt;</c> in their own module
/// assembly; MassTransit's <c>AddConsumers(assembly)</c> auto-wires them.
/// </summary>
public sealed record CommentDeletedIntegrationEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid DeletedBy,
    DateTime OccurredAt);
