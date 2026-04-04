using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ImportExport.DTOs;
using Starter.Domain.ImportExport.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetImportJobById;

internal sealed class GetImportJobByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetImportJobByIdQuery, Result<ImportJobDto>>
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
