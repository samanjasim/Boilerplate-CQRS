namespace Starter.Abstractions.Events.CommentsActivity;

/// <summary>
/// Published when a comment is created on any commentable entity. Consumers
/// (analytics, AI enrichment, audit, etc.) register an
/// <c>IConsumer&lt;CommentCreatedIntegrationEvent&gt;</c> in their own module
/// assembly; MassTransit's <c>AddConsumers(assembly)</c> auto-wires them.
/// </summary>
public sealed record CommentCreatedIntegrationEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid AuthorId,
    string Body,
    string? MentionsJson,
    Guid? ParentCommentId,
    DateTime OccurredAt);
