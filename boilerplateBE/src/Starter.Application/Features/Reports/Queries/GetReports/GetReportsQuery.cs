using MediatR;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.Reports.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReports;

public sealed record GetReportsQuery : PaginationQuery, IRequest<Result<PaginatedList<ReportDto>>>
{
    public string? Status { get; init; }
    public string? ReportType { get; init; }
}
