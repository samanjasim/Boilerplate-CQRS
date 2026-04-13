using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Entities;

public sealed class CommentAttachment : BaseEntity
{
    public Guid CommentId { get; private set; }
    public Guid FileMetadataId { get; private set; }
    public int SortOrder { get; private set; }

    private CommentAttachment() { }

    public static CommentAttachment Create(
        Guid commentId,
        Guid fileMetadataId,
        int sortOrder = 0)
    {
        return new CommentAttachment
        {
            Id = Guid.NewGuid(),
            CommentId = commentId,
            FileMetadataId = fileMetadataId,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }
}
