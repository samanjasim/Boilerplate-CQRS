using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="ICommentService"/> registered when the
/// Comments &amp; Activity module is not installed. All operations are silent
/// no-ops so callers need no module-awareness.
/// </summary>
public sealed class NullCommentService(ILogger<NullCommentService> logger) : ICommentService
{
    public Task<Guid> AddCommentAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        Guid authorId,
        string body,
        string? mentionsJson,
        IReadOnlyList<Guid>? attachmentFileIds,
        Guid? parentCommentId = null,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Comment add skipped — Comments module not installed (entity: {EntityType}, id: {EntityId})",
            entityType, entityId);
        return Task.FromResult(Guid.Empty);
    }

    public Task EditCommentAsync(
        Guid commentId,
        string newBody,
        string? newMentionsJson,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Comment edit skipped — Comments module not installed (comment: {CommentId})",
            commentId);
        return Task.CompletedTask;
    }

    public Task DeleteCommentAsync(
        Guid commentId,
        Guid deletedBy,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Comment delete skipped — Comments module not installed (comment: {CommentId})",
            commentId);
        return Task.CompletedTask;
    }

    public Task<CommentSummary?> GetByIdAsync(
        Guid commentId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Comment lookup skipped — Comments module not installed (comment: {CommentId})",
            commentId);
        return Task.FromResult<CommentSummary?>(null);
    }

    public Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(
        string entityType,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Comment list skipped — Comments module not installed (entity: {EntityType}, id: {EntityId})",
            entityType, entityId);
        return Task.FromResult<IReadOnlyList<CommentSummary>>([]);
    }
}
