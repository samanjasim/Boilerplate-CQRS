using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Commands.UpdateFileMetadata;

internal sealed class UpdateFileMetadataCommandHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<UpdateFileMetadataCommand, Result<FileDto>>
{
    public async Task<Result<FileDto>> Handle(UpdateFileMetadataCommand request, CancellationToken cancellationToken)
    {
        var metadata = await context.FileMetadata
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (metadata is null)
            return Result.Failure<FileDto>(FileErrors.NotFound(request.Id));

        if (request.Description is not null)
            metadata.Description = request.Description;

        if (request.Category.HasValue)
            metadata.Category = request.Category.Value;

        if (request.Tags is not null)
            metadata.Tags = request.Tags.Length > 0 ? string.Join(",", request.Tags) : null;

        await context.SaveChangesAsync(cancellationToken);

        var url = await fileService.GetUrlAsync(metadata.Id, cancellationToken);

        return Result.Success(metadata.ToDto(url));
    }
}
