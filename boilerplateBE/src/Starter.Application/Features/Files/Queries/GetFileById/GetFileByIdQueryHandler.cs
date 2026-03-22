using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Queries.GetFileById;

internal sealed class GetFileByIdQueryHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<GetFileByIdQuery, Result<FileDto>>
{
    public async Task<Result<FileDto>> Handle(GetFileByIdQuery request, CancellationToken cancellationToken)
    {
        var metadata = await context.FileMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (metadata is null)
            return Result.Failure<FileDto>(FileErrors.NotFound(request.Id));

        var url = await fileService.GetUrlAsync(metadata.Id, cancellationToken);

        return Result.Success(metadata.ToDto(url));
    }
}
