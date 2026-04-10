using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Module.ImportExport.Domain.Errors;
using Starter.Module.ImportExport.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportJobById;

internal sealed class GetImportJobByIdQueryHandler(
    ImportExportDbContext context) : IRequestHandler<GetImportJobByIdQuery, Result<ImportJobDto>>
{
    public async Task<Result<ImportJobDto>> Handle(
        GetImportJobByIdQuery request, CancellationToken cancellationToken)
    {
        var job = await context.ImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);

        if (job is null)
            return Result.Failure<ImportJobDto>(ImportExportErrors.JobNotFound);

        return Result.Success(job.ToDto());
    }
}
