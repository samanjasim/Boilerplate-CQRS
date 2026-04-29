using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Reports.DTOs;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

internal sealed class GetReportStatusCountsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetReportStatusCountsQuery, Result<ReportStatusCountsDto>>
{
    public async Task<Result<ReportStatusCountsDto>> Handle(
        GetReportStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<ReportStatusCountsDto>(UserErrors.Unauthorized());

        var query = context.Set<ReportRequest>()
            .AsNoTracking()
            .AsQueryable();

        // Keep the aggregate scoped to the same data surface as the report list.
        if (currentUserService.TenantId is not null)
        {
            query = query.Where(r =>
                r.RequestedBy == userId.Value || r.TenantId == currentUserService.TenantId);
        }

        var counts = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountFor(ReportStatus status) =>
            counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var dto = new ReportStatusCountsDto(
            Pending: CountFor(ReportStatus.Pending),
            Processing: CountFor(ReportStatus.Processing),
            Completed: CountFor(ReportStatus.Completed),
            Failed: CountFor(ReportStatus.Failed)
        );

        return Result.Success(dto);
    }
}
