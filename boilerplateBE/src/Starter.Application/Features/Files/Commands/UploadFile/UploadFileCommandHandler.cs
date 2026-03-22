using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Commands.UploadFile;

internal sealed class UploadFileCommandHandler(
    IFileService fileService) : IRequestHandler<UploadFileCommand, Result<FileDto>>
{
    public async Task<Result<FileDto>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        var metadata = await fileService.UploadAsync(
            request.Stream,
            request.FileName,
            request.ContentType,
            request.Size,
            request.Category,
            request.EntityId,
            request.EntityType,
            request.Description,
            request.Tags,
            request.IsPublic,
            cancellationToken);

        var url = await fileService.GetUrlAsync(metadata.Id, cancellationToken);

        return Result.Success(metadata.ToDto(url));
    }
}
