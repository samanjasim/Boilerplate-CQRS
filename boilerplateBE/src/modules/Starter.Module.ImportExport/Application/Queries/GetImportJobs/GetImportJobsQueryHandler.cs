using Starter.Abstractions.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Models;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Module.ImportExport.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportJobs;

internal sealed class GetImportJobsQueryHandler(
    ImportExportDbContext context) : IRequestHandler<GetImportJobsQuery, Result<PaginatedList<ImportJobDto>>>
{
    public async Task<Result<PaginatedList<ImportJobDto>>> Handle(
        GetImportJobsQuery request, CancellationToken cancellationToken)
    {
        var query = context.ImportJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(j => j.ToDto()).ToList();

        return Result.Success(PaginatedList<ImportJobDto>.Create(
            dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize));
    }
}
