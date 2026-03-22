using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Errors;
using PermissionConstants = Starter.Shared.Constants.Permissions;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Commands.DeleteFile;

internal sealed class DeleteFileCommandHandler(
    IApplicationDbContext context,
    IFileService fileService,
    ICurrentUserService currentUserService) : IRequestHandler<DeleteFileCommand, Result>
{
    public async Task<Result> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        // Global query filter scopes by tenant; defense-in-depth check below
        var file = await context.FileMetadata
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (file is null)
            return Result.Failure(FileErrors.NotFound(request.Id));

        if (file.TenantId != currentUserService.TenantId && !currentUserService.HasPermission(PermissionConstants.Files.Manage))
            return Result.Failure(Error.Unauthorized());

        await fileService.DeleteAsync(request.Id, cancellationToken);

        return Result.Success();
    }
}
