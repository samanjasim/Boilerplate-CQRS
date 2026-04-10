using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.ImportExport.Domain.Errors;
using Starter.Module.ImportExport.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportErrorReport;

internal sealed class GetImportErrorReportQueryHandler(
    ImportExportDbContext context,
    IFileService fileService) : IRequestHandler<GetImportErrorReportQuery, Result<string>>
{
    public async Task<Result<string>> Handle(
        GetImportErrorReportQuery request, CancellationToken cancellationToken)
    {
        var job = await context.ImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);

        if (job is null)
            return Result.Failure<string>(ImportExportErrors.JobNotFound);

        if (!job.ResultsFileId.HasValue)
            return Result.Failure<string>(Error.NotFound("ImportExport.NoErrorReport", "No error report is available for this job."));

        var url = await fileService.GetUrlAsync(job.ResultsFileId.Value, cancellationToken);

        return Result.Success(url);
    }
}
