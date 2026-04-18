using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Events;

public sealed record CommentCreatedEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid AuthorId,
    string? MentionsJson,
    Guid? ParentCommentId,
    string? Body = null) : DomainEventBase;
