using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Errors;
using PermissionConstants = Starter.Shared.Constants.Permissions;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Queries.GetFileUrl;

internal sealed class GetFileUrlQueryHandler(
    IApplicationDbContext context,
    IFileService fileService,
    ICurrentUserService currentUserService) : IRequestHandler<GetFileUrlQuery, Result<string>>
{
    public async Task<Result<string>> Handle(GetFileUrlQuery request, CancellationToken cancellationToken)
    {
        // Check access for private files
        var metadata = await context.FileMetadata.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (metadata is null)
            return Result.Failure<string>(FileErrors.NotFound(request.Id));
        if (!metadata.IsPublic && metadata.UploadedBy != currentUserService.UserId && !currentUserService.HasPermission(PermissionConstants.Files.Manage))
            return Result.Failure<string>(FileErrors.AccessDenied());

        var url = await fileService.GetUrlAsync(request.Id, cancellationToken);

        return Result.Success(url);
    }
}
