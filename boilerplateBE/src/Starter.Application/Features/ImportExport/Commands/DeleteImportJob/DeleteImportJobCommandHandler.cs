using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ImportExport.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Commands.DeleteImportJob;

internal sealed class DeleteImportJobCommandHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<DeleteImportJobCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        DeleteImportJobCommand request, CancellationToken cancellationToken)
    {
        var job = await context.ImportJobs
            .FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);

        if (job is null)
            return Result.Failure<Unit>(ImportExportErrors.JobNotFound);

        // Delete the source file
        try
        {
            await fileService.DeleteAsync(job.FileId, cancellationToken);
        }
        catch
        {
            // Best-effort; continue with job deletion
        }

        // Delete the error report file if present
        if (job.ResultsFileId.HasValue)
        {
            try
            {
                await fileService.DeleteAsync(job.ResultsFileId.Value, cancellationToken);
            }
            catch
            {
                // Best-effort; continue with job deletion
            }
        }

        context.ImportJobs.Remove(job);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(Unit.Value);
    }
}
