using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Errors;
using Starter.Domain.Common.Events;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Application.Features.Files.Commands.DeleteFile;

internal sealed class DeleteFileCommandHandler(
    IApplicationDbContext context,
    IFileService fileService,
    IResourceAccessService accessService,
    ICurrentUserService currentUserService,
    IPublisher publisher) : IRequestHandler<DeleteFileCommand, Result>
{
    public async Task<Result> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        // Global query filter scopes by tenant; defense-in-depth check below
        var file = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (file is null)
            return Result.Failure(FileErrors.NotFound(request.Id));

        if (file.TenantId != currentUserService.TenantId && !currentUserService.HasPermission(Starter.Shared.Constants.Permissions.Files.Manage))
            return Result.Failure(Error.Unauthorized());

        var fileId = file.Id;
        var tenantId = file.TenantId;
        var fileName = file.FileName;

        await fileService.DeleteAsync(request.Id, cancellationToken);

        await accessService.RevokeAllForResourceAsync(ResourceTypes.File, fileId, cancellationToken);

        await publisher.Publish(
            new FileDeletedEvent(fileId, tenantId, fileName),
            cancellationToken);

        return Result.Success();
    }
}
