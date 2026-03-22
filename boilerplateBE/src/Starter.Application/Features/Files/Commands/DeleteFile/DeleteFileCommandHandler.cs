using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Commands.DeleteFile;

internal sealed class DeleteFileCommandHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<DeleteFileCommand, Result>
{
    public async Task<Result> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        var exists = await context.FileMetadata
            .AnyAsync(f => f.Id == request.Id, cancellationToken);

        if (!exists)
            return Result.Failure(FileErrors.NotFound(request.Id));

        await fileService.DeleteAsync(request.Id, cancellationToken);

        return Result.Success();
    }
}
