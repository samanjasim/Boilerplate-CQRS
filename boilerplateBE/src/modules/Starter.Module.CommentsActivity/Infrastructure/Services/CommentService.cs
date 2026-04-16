using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Infrastructure.Services;

public sealed class CommentService(
    CommentsActivityDbContext context,
    ICommentableEntityRegistry registry,
    IServiceProvider services,
    ILogger<CommentService> logger) : ICommentService
{
    public async Task<Guid> AddCommentAsync(
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
        var effectiveTenantId = await TenantResolution.ResolveEffectiveTenantIdAsync(
            registry, services, logger, entityType, entityId, tenantId, ct);

        var comment = Comment.Create(
            effectiveTenantId, entityType, entityId, parentCommentId,
            authorId, body, mentionsJson);

        context.Comments.Add(comment);

        if (attachmentFileIds is { Count: > 0 })
        {
            for (var i = 0; i < attachmentFileIds.Count; i++)
            {
                var attachment = CommentAttachment.Create(comment.Id, attachmentFileIds[i], i);
                context.CommentAttachments.Add(attachment);
            }
        }

        await context.SaveChangesAsync(ct);
        return comment.Id;
    }

    public async Task EditCommentAsync(
        Guid commentId,
        string newBody,
        string? newMentionsJson,
        Guid editorId,
        CancellationToken ct = default)
    {
        var comment = await context.Comments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);

        if (comment is null) return;

        if (!await IsTenantReconciledAsync(comment.EntityType, comment.EntityId, comment.TenantId, ct))
            return;

        comment.Edit(newBody, newMentionsJson, editorId);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteCommentAsync(
        Guid commentId,
        Guid deletedBy,
        CancellationToken ct = default)
    {
        var comment = await context.Comments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);

        if (comment is null || comment.IsDeleted) return;

        if (!await IsTenantReconciledAsync(comment.EntityType, comment.EntityId, comment.TenantId, ct))
            return;

        comment.SoftDelete(deletedBy);
        await context.SaveChangesAsync(ct);
    }

    private async Task<bool> IsTenantReconciledAsync(
        string entityType,
        Guid entityId,
        Guid? persistedTenantId,
        CancellationToken ct)
    {
        var resolved = await TenantResolution.ResolveEffectiveTenantIdAsync(
            registry, services, logger, entityType, entityId, persistedTenantId, ct);
        return resolved == persistedTenantId;
    }

    public async Task<CommentSummary?> GetByIdAsync(
        Guid commentId,
        CancellationToken ct = default)
    {
        return await context.Comments
            .AsNoTracking()
            .Where(c => c.Id == commentId)
            .Select(c => new CommentSummary(
                c.Id, c.EntityType, c.EntityId, c.AuthorId,
                c.Body, c.MentionsJson, c.ParentCommentId,
                c.IsDeleted, c.CreatedAt, c.ModifiedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(
        string entityType,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return await context.Comments
            .AsNoTracking()
            .Where(c => c.EntityType == entityType && c.EntityId == entityId)
            .OrderBy(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentSummary(
                c.Id, c.EntityType, c.EntityId, c.AuthorId,
                c.Body, c.MentionsJson, c.ParentCommentId,
                c.IsDeleted, c.CreatedAt, c.ModifiedAt))
            .ToListAsync(ct);
    }
}
