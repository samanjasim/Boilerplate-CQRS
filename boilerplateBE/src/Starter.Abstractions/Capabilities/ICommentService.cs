namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Adds, edits, deletes, and queries comments on any commentable entity.
/// Implemented by the Comments &amp; Activity module; when the module is not
/// installed, a silent no-op Null Object is registered in core.
/// </summary>
public interface ICommentService : ICapability
{
    Task<Guid> AddCommentAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        Guid authorId,
        string body,
        string? mentionsJson,
        IReadOnlyList<Guid>? attachmentFileIds,
        Guid? parentCommentId = null,
        CancellationToken ct = default);

    Task EditCommentAsync(
        Guid commentId,
        string newBody,
        string? newMentionsJson,
        Guid editorId,
        CancellationToken ct = default);

    Task DeleteCommentAsync(
        Guid commentId,
        Guid deletedBy,
        CancellationToken ct = default);

    Task<CommentSummary?> GetByIdAsync(
        Guid commentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(
        string entityType,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default);
}
