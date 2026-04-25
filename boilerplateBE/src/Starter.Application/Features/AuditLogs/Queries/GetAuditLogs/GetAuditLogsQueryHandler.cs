using Starter.Abstractions.Paging;
using Starter.Application.Common.Extensions;
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

        // Query-string binding gives DateTime.Kind=Unspecified; Npgsql rejects it
        // for `timestamp with time zone` columns. Normalize to UTC before filtering.
        if (request.DateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(request.DateFrom.Value, DateTimeKind.Utc);
            query = query.Where(a => a.PerformedAt >= from);
        }

        if (request.DateTo.HasValue)
        {
            // Inclusive end-of-day so a single-day filter (from=X, to=X) returns same-day rows.
            var to = DateTime.SpecifyKind(request.DateTo.Value, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
            query = query.Where(a => a.PerformedAt <= to);
        }

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

        var paginatedList = await projected.ToPaginatedListAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paginatedList);
    }
}
