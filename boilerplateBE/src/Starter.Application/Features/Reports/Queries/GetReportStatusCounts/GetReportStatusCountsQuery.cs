using MediatR;
using Starter.Application.Features.Reports.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

public sealed record GetReportStatusCountsQuery() : IRequest<Result<ReportStatusCountsDto>>;
