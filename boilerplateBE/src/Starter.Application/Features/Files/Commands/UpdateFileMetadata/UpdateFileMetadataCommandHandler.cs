using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Errors;
using Starter.Shared.Constants;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Commands.UpdateFileMetadata;

internal sealed class UpdateFileMetadataCommandHandler(
    IApplicationDbContext context,
    IFileService fileService,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateFileMetadataCommand, Result<FileDto>>
{
    public async Task<Result<FileDto>> Handle(UpdateFileMetadataCommand request, CancellationToken cancellationToken)
    {
        // Global query filter scopes by tenant; defense-in-depth check below
        var metadata = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (metadata is null)
            return Result.Failure<FileDto>(FileErrors.NotFound(request.Id));

        if (metadata.TenantId != currentUserService.TenantId && !currentUserService.HasPermission(Starter.Shared.Constants.Permissions.Files.Manage))
            return Result.Failure<FileDto>(Error.Unauthorized());

        var tags = request.Tags is { Length: > 0 } ? string.Join(",", request.Tags) : request.Tags is not null ? null : metadata.Tags;

        metadata.UpdateMetadata(request.Description, request.Category, tags);

        await context.SaveChangesAsync(cancellationToken);

        var url = await fileService.GetUrlAsync(metadata.Id, cancellationToken);

        return Result.Success(metadata.ToDto(url));
    }
}
