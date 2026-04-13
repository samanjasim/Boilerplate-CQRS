using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Events;

public sealed record CommentEditedEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId) : DomainEventBase;
