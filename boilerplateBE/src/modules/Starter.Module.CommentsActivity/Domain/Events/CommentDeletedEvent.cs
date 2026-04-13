using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Events;

public sealed record CommentDeletedEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid DeletedBy) : DomainEventBase;
