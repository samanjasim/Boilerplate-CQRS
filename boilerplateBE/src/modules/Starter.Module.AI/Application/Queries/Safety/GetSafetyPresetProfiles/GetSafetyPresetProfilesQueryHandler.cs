using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Safety.GetSafetyPresetProfiles;

internal sealed class GetSafetyPresetProfilesQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetSafetyPresetProfilesQuery, Result<PaginatedList<SafetyPresetProfileDto>>>
{
    public async Task<Result<PaginatedList<SafetyPresetProfileDto>>> Handle(
        GetSafetyPresetProfilesQuery q, CancellationToken ct)
    {
        // AiSafetyPresetProfile has NO global tenant query filter (platform defaults
        // are stored as TenantId == null rows shared across tenants). Apply scoping
        // manually: SuperAdmin (TenantId == null) sees all rows; tenant admin sees
        // platform defaults + their tenant overrides.
        IQueryable<AiSafetyPresetProfile> source = db.AiSafetyPresetProfiles.AsNoTracking();

        if (currentUser.TenantId is { } tenantId)
            source = source.Where(p => p.TenantId == null || p.TenantId == tenantId);

        source = source
            .OrderBy(p => p.TenantId == null ? 0 : 1) // platform defaults first
            .ThenBy(p => p.Preset)
            .ThenBy(p => p.Provider);

        var page = q.Page < 1 ? 1 : q.Page;
        var size = q.PageSize is < 1 or > 200 ? 20 : q.PageSize;

        var total = await source.CountAsync(ct);
        var items = await source
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new SafetyPresetProfileDto(
                p.Id,
                p.TenantId,
                p.Preset,
                p.Provider,
                p.CategoryThresholdsJson,
                p.BlockedCategoriesJson,
                p.FailureMode,
                p.RedactPii,
                p.Version,
                p.IsActive,
                p.CreatedAt,
                p.ModifiedAt))
            .ToListAsync(ct);

        return Result.Success(new PaginatedList<SafetyPresetProfileDto>(items, total, page, size));
    }
}
