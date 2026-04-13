using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Entities;

public sealed class CommentReaction : BaseEntity
{
    public Guid CommentId { get; private set; }
    public Guid UserId { get; private set; }
    public string ReactionType { get; private set; } = default!;

    private CommentReaction() { }

    public static CommentReaction Create(
        Guid commentId,
        Guid userId,
        string reactionType)
    {
        return new CommentReaction
        {
            Id = Guid.NewGuid(),
            CommentId = commentId,
            UserId = userId,
            ReactionType = reactionType.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
