namespace Starter.Abstractions.Events.CommentsActivity;

/// <summary>
/// Published when a reaction is added to or removed from a comment. Consumers
/// register an <c>IConsumer&lt;ReactionToggledIntegrationEvent&gt;</c> in their
/// own module assembly; MassTransit's <c>AddConsumers(assembly)</c> auto-wires
/// them. <c>IsAdded</c> is true when the reaction was added, false when removed.
/// </summary>
public sealed record ReactionToggledIntegrationEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid UserId,
    string ReactionType,
    bool IsAdded,
    DateTime OccurredAt);
