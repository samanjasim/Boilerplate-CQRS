using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Domain.Common;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogs;

internal sealed class GetAuditLogsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetAuditLogsQuery, Result<PaginatedList<AuditLogDto>>>
{
    public async Task<Result<PaginatedList<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Set<AuditLog>()
            .AsNoTracking()
            .AsQueryable();

        if (request.EntityType.HasValue)
            query = query.Where(a => a.EntityType == request.EntityType.Value);

        if (request.EntityId.HasValue)
            query = query.Where(a => a.EntityId == request.EntityId.Value);

        if (request.Action.HasValue)
            query = query.Where(a => a.Action == request.Action.Value);

        if (request.PerformedBy.HasValue)
            query = query.Where(a => a.PerformedBy == request.PerformedBy.Value);

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.PerformedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(a => a.PerformedAt <= request.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                (a.PerformedByName != null && a.PerformedByName.ToLower().Contains(searchTerm)) ||
                (a.CorrelationId != null && a.CorrelationId.ToLower().Contains(searchTerm)));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "entitytype" => request.SortDescending
                ? query.OrderByDescending(a => a.EntityType)
                : query.OrderBy(a => a.EntityType),
            "action" => request.SortDescending
                ? query.OrderByDescending(a => a.Action)
                : query.OrderBy(a => a.Action),
            _ => request.SortDescending
                ? query.OrderByDescending(a => a.PerformedAt)
                : query.OrderBy(a => a.PerformedAt)
        };

        var projected = query.Select(a => new AuditLogDto(
            a.Id,
            a.EntityType.ToString(),
            a.EntityId,
            a.Action.ToString(),
            a.Changes,
            a.PerformedBy,
            a.PerformedByName,
            a.PerformedAt,
            a.IpAddress,
            a.CorrelationId));

        var paginatedList = await PaginatedList<AuditLogDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paginatedList);
    }
}
