using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Reports.DTOs;
using Starter.Domain.Common.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReports;

internal sealed class GetReportsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetReportsQuery, Result<PaginatedList<ReportDto>>>
{
    public async Task<Result<PaginatedList<ReportDto>>> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<PaginatedList<ReportDto>>(UserErrors.Unauthorized());

        var query = context.ReportRequests
            .AsNoTracking()
            .AsQueryable();

        // Platform admin sees all (tenant filter handles global scoping).
        // Tenant user sees own reports + shared tenant reports.
        if (currentUserService.TenantId is not null)
        {
            query = query.Where(r =>
                r.RequestedBy == userId.Value || r.TenantId == currentUserService.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ReportStatus.FromName(request.Status);
            if (status is not null)
                query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ReportType))
        {
            var reportType = Domain.Common.Enums.ReportType.FromName(request.ReportType);
            if (reportType is not null)
                query = query.Where(r => r.ReportType == reportType);
        }

        query = query.OrderByDescending(r => r.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var reports = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var reportDtos = reports.Select(r => r.ToDto()).ToList();

        var paginatedList = PaginatedList<ReportDto>.Create(
            reportDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(paginatedList);
    }
}
