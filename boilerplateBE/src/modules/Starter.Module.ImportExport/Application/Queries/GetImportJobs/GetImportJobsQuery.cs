using Starter.Abstractions.Paging;
using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportJobs;

public sealed record GetImportJobsQuery(int PageNumber = 1, int PageSize = 20)
    : IRequest<Result<PaginatedList<ImportJobDto>>>;
