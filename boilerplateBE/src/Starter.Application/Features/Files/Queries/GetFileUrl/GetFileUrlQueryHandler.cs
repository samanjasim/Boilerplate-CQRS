using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Errors;
using Starter.Shared.Constants;
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
        var metadata = await context.Set<FileMetadata>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (metadata is null)
            return Result.Failure<string>(FileErrors.NotFound(request.Id));
        if (metadata.Visibility != ResourceVisibility.Public
            && metadata.UploadedBy != currentUserService.UserId
            && !currentUserService.HasPermission(Starter.Shared.Constants.Permissions.Files.Manage))
            return Result.Failure<string>(FileErrors.AccessDenied());

        var url = await fileService.GetUrlAsync(request.Id, cancellationToken);

        return Result.Success(url);
    }
}
