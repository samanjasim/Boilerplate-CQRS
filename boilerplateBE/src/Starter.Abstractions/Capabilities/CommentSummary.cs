namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Lightweight projection of a comment, returned by <see cref="ICommentService"/>
/// so consumers never depend on the domain entity directly.
/// </summary>
public sealed record CommentSummary(
    Guid Id,
    string EntityType,
    Guid EntityId,
    Guid AuthorId,
    string Body,
    string? MentionsJson,
    Guid? ParentCommentId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
