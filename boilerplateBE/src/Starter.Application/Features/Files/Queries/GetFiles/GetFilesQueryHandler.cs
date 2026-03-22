using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Queries.GetFiles;

internal sealed class GetFilesQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetFilesQuery, Result<PaginatedList<FileDto>>>
{
    public async Task<Result<PaginatedList<FileDto>>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        var query = context.FileMetadata.AsNoTracking();

        if (request.Category.HasValue)
            query = query.Where(f => f.Category == request.Category.Value);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(f => f.EntityType == request.EntityType);

        if (request.EntityId.HasValue)
            query = query.Where(f => f.EntityId == request.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(f =>
                f.FileName.ToLower().Contains(term) ||
                (f.Description != null && f.Description.ToLower().Contains(term)) ||
                (f.Tags != null && f.Tags.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(f => f.CreatedAt);

        var projectedQuery = query.Select(f => new FileDto(
            f.Id,
            f.FileName,
            f.ContentType,
            f.Size,
            f.Category,
            f.Tags,
            f.TenantId,
            f.UploadedBy,
            f.IsPublic,
            f.Description,
            f.EntityType,
            f.EntityId,
            f.CreatedAt,
            null));

        var paginatedList = await PaginatedList<FileDto>.CreateAsync(
            projectedQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paginatedList);
    }
}
