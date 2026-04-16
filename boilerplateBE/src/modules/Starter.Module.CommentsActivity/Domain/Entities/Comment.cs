using Starter.Domain.Common;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Domain.Entities;

public sealed class Comment : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = default!;
    public string? MentionsJson { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public Comment? ParentComment { get; private set; }
    public ICollection<Comment> Replies { get; private set; } = [];
    public ICollection<CommentAttachment> Attachments { get; private set; } = [];
    public ICollection<CommentReaction> Reactions { get; private set; } = [];

    private Comment() { }

    private Comment(
        Guid id,
        Guid? tenantId,
        string entityType,
        Guid entityId,
        Guid? parentCommentId,
        Guid authorId,
        string body,
        string? mentionsJson) : base(id)
    {
        TenantId = tenantId;
        EntityType = entityType;
        EntityId = entityId;
        ParentCommentId = parentCommentId;
        AuthorId = authorId;
        Body = body;
        MentionsJson = mentionsJson;
        IsDeleted = false;
    }

    public static Comment Create(
        Guid? tenantId,
        string entityType,
        Guid entityId,
        Guid? parentCommentId,
        Guid authorId,
        string body,
        string? mentionsJson)
    {
        var comment = new Comment(
            Guid.NewGuid(),
            tenantId,
            entityType.Trim(),
            entityId,
            parentCommentId,
            authorId,
            body.Trim(),
            mentionsJson);

        comment.RaiseDomainEvent(new CommentCreatedEvent(
            comment.Id,
            comment.EntityType,
            comment.EntityId,
            tenantId,
            authorId,
            mentionsJson,
            parentCommentId));

        return comment;
    }

    public void Edit(string newBody, string? newMentionsJson, Guid editorId)
    {
        Body = newBody.Trim();
        MentionsJson = newMentionsJson;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new CommentEditedEvent(
            Id, EntityType, EntityId, TenantId, editorId));
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
        Body = string.Empty;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new CommentDeletedEvent(
            Id, EntityType, EntityId, TenantId, deletedBy));
    }

    public void RecordReactionToggle(Guid userId, string reactionType, bool added)
    {
        RaiseDomainEvent(new ReactionToggledEvent(
            Id, EntityType, EntityId, TenantId, userId, reactionType, added));
    }
}
