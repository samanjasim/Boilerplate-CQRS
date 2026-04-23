using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetComments;

internal sealed class GetCommentsQueryHandler(
    CommentsActivityDbContext context,
    IUserReader userReader,
    IFileReader fileReader,
    IFileService fileService,
    ICurrentUserService currentUser) : IRequestHandler<GetCommentsQuery, Result<PaginatedList<CommentDto>>>
{
    public async Task<Result<PaginatedList<CommentDto>>> Handle(
        GetCommentsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Comments
            .AsNoTracking()
            .Where(c => c.EntityType == request.EntityType && c.EntityId == request.EntityId)
            .Where(c => c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt);

        var page = await query.ToPaginatedListAsync(
            request.PageNumber, request.PageSize, cancellationToken);

        var commentIds = page.Items.Select(c => c.Id).ToList();

        // Load replies for all top-level comments on this page
        var replies = await context.Comments
            .AsNoTracking()
            .Where(c => c.ParentCommentId != null && commentIds.Contains(c.ParentCommentId.Value))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        // Load attachments for all comments (top-level + replies)
        var allCommentIds = commentIds.Concat(replies.Select(r => r.Id)).ToList();
        var attachments = await context.CommentAttachments
            .AsNoTracking()
            .Where(a => allCommentIds.Contains(a.CommentId))
            .ToListAsync(cancellationToken);

        // Load reactions for all comments
        var reactions = await context.CommentReactions
            .AsNoTracking()
            .Where(r => allCommentIds.Contains(r.CommentId))
            .ToListAsync(cancellationToken);

        // Resolve file metadata for attachments
        var fileIds = attachments.Select(a => a.FileMetadataId).Distinct();
        var fileSummaries = await fileReader.GetManyAsync(fileIds, cancellationToken);
        var fileMap = fileSummaries.ToDictionary(f => f.Id);

        // Resolve file URLs
        var fileUrlMap = new Dictionary<Guid, string>();
        foreach (var fid in fileMap.Keys)
        {
            fileUrlMap[fid] = await fileService.GetUrlAsync(fid, cancellationToken);
        }

        // Resolve all author names
        var authorIds = page.Items.Select(c => c.AuthorId)
            .Concat(replies.Select(r => r.AuthorId))
            .Distinct();
        var users = await userReader.GetManyAsync(authorIds, cancellationToken);
        var userMap = users.ToDictionary(u => u.Id);

        var currentUserId = currentUser.UserId;

        var dtos = page.Items.Select(c => CommentDtoMapper.MapComment(
            c, replies, attachments, reactions, userMap, fileMap, fileUrlMap, currentUserId)).ToList();

        var result = PaginatedList<CommentDto>.Create(
            dtos.AsReadOnly(), page.TotalCount, page.PageNumber, page.PageSize);

        return Result.Success(result);
    }
}
