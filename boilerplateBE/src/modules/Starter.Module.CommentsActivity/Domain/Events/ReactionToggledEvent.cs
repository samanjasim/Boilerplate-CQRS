using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Events;

public sealed record ReactionToggledEvent(
    Guid CommentId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid UserId,
    string ReactionType,
    bool Added) : DomainEventBase;
