using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Starter.Domain.Identity.Entities;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Queries.GetFiles;

internal sealed class GetFilesQueryHandler(
    IApplicationDbContext context,
    IResourceAccessService access,
    ICurrentUserService currentUser) : IRequestHandler<GetFilesQuery, Result<PaginatedList<FileDto>>>
{
    public async Task<Result<PaginatedList<FileDto>>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        var resolution = await access.ResolveAccessibleResourcesAsync(currentUser, ResourceTypes.File, cancellationToken);
        var uid = currentUser.UserId;

        var query = context.Set<FileMetadata>().AsNoTracking()
            .Where(f => f.Status == FileStatus.Permanent);

        if (!resolution.IsAdminBypass && uid.HasValue)
        {
            var userId = uid.Value;
            var grantedIds = resolution.ExplicitGrantedResourceIds;
            query = query.Where(f =>
                f.Visibility == ResourceVisibility.TenantWide
                || f.Visibility == ResourceVisibility.Public
                || f.UploadedBy == userId
                || grantedIds.Contains(f.Id));
        }

        if (request.Category.HasValue)
            query = query.Where(f => f.Category == request.Category.Value);

        if (!string.IsNullOrWhiteSpace(request.Origin) && Enum.TryParse<FileOrigin>(request.Origin, true, out var origin))
            query = query.Where(f => f.Origin == origin);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(f => f.EntityType == request.EntityType);

        if (request.EntityId.HasValue)
            query = query.Where(f => f.EntityId == request.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(request.View) && uid.HasValue)
        {
            var userId = uid.Value;
            var grantedIds = resolution.ExplicitGrantedResourceIds;
            query = request.View.ToLowerInvariant() switch
            {
                "mine" => query.Where(f => f.UploadedBy == userId),
                "shared" => query.Where(f => f.UploadedBy != userId
                                             && (grantedIds.Contains(f.Id)
                                                 || f.Visibility == ResourceVisibility.TenantWide)),
                "public" => query.Where(f => f.Visibility == ResourceVisibility.Public),
                _ => query,
            };
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(f =>
                f.FileName.ToLower().Contains(term) ||
                (f.Description != null && f.Description.ToLower().Contains(term)) ||
                (f.Tags != null && f.Tags.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(f => f.CreatedAt);

        var projectedQuery = from f in query
            join u in context.Set<User>() on f.UploadedBy equals u.Id into users
            from u in users.DefaultIfEmpty()
            select new FileDto(
                f.Id,
                f.FileName,
                f.ContentType,
                f.Size,
                f.Category.ToString(),
                f.Tags,
                f.TenantId,
                f.UploadedBy,
                u != null ? u.FullName.FirstName + " " + u.FullName.LastName : null,
                f.Visibility,
                f.Description,
                f.EntityType,
                f.EntityId,
                f.CreatedAt,
                null,
                f.Status.ToString(),
                f.Origin.ToString(),
                f.ExpiresAt);

        var paginatedList = await PaginatedList<FileDto>.CreateAsync(
            projectedQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paginatedList);
    }
}
