using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.ImportExport.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetImportJobs;

public sealed record GetImportJobsQuery(int PageNumber = 1, int PageSize = 20)
    : IRequest<Result<PaginatedList<ImportJobDto>>>;
