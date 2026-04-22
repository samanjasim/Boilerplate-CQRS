using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Queries.GetFileUrl;

internal sealed class GetFileUrlQueryHandler(
    IApplicationDbContext context,
    IFileService fileService,
    IResourceAccessService access,
    ICurrentUserService currentUser) : IRequestHandler<GetFileUrlQuery, Result<string>>
{
    public async Task<Result<string>> Handle(GetFileUrlQuery request, CancellationToken cancellationToken)
    {
        var file = await context.Set<FileMetadata>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (file is null)
            return Result.Failure<string>(FileErrors.NotFound(request.Id));

        if (file.Visibility == ResourceVisibility.Public)
        {
            // public files accessible to anyone
        }
        else if (file.Visibility == ResourceVisibility.TenantWide && file.TenantId == currentUser.TenantId)
        {
            // tenant-wide files accessible to any user in same tenant
        }
        else if (file.UploadedBy == currentUser.UserId)
        {
            // owner bypass
        }
        else if (!await access.CanAccessAsync(currentUser, ResourceTypes.File, file.Id, AccessLevel.Viewer, cancellationToken))
        {
            return Result.Failure<string>(FileErrors.AccessDenied());
        }

        var url = await fileService.GetUrlAsync(request.Id, cancellationToken);
        return Result.Success(url);
    }
}
