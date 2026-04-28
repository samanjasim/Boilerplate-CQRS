using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Safety.GetModerationEvents;

internal sealed class GetModerationEventsQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetModerationEventsQuery, Result<PaginatedList<ModerationEventDto>>>
{
    public async Task<Result<PaginatedList<ModerationEventDto>>> Handle(
        GetModerationEventsQuery q, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Result.Failure<PaginatedList<ModerationEventDto>>(Error.Unauthorized());

        // Tenant scope is enforced by the global query filter on AiModerationEvent.
        var source = db.AiModerationEvents.AsNoTracking();

        if (q.From is { } from)
        {
            var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            source = source.Where(e => e.CreatedAt >= fromUtc);
        }
        if (q.To is { } to)
        {
            var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);
            source = source.Where(e => e.CreatedAt <= toUtc);
        }
        if (q.Outcome is { } o) source = source.Where(e => e.Outcome == o);
        if (q.Stage is { } st) source = source.Where(e => e.Stage == st);
        if (q.AssistantId is { } a) source = source.Where(e => e.AssistantId == a);

        source = source.OrderByDescending(e => e.CreatedAt);

        var page = q.Page < 1 ? 1 : q.Page;
        var size = q.PageSize is < 1 or > 200 ? 50 : q.PageSize;

        var total = await source.CountAsync(ct);
        var items = await source
            .Skip((page - 1) * size)
            .Take(size)
            .Select(e => new ModerationEventDto(
                e.Id,
                e.TenantId,
                e.AssistantId,
                e.Stage,
                e.Preset,
                e.Outcome,
                e.CategoriesJson,
                e.Provider,
                e.BlockedReason,
                e.LatencyMs,
                e.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PaginatedList<ModerationEventDto>(items, total, page, size));
    }
}
