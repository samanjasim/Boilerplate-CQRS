namespace Starter.Abstractions.Events.CommentsActivity;

/// <summary>
/// Published when a comment's body is edited. Consumers register an
/// <c>IConsumer&lt;CommentEditedIntegrationEvent&gt;</c> in their own module
/// assembly; MassTransit's <c>AddConsumers(assembly)</c> auto-wires them.
/// </summary>
public sealed record CommentEditedIntegrationEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid EditorId,
    string NewBody,
    string? NewMentionsJson,
    DateTime OccurredAt);
