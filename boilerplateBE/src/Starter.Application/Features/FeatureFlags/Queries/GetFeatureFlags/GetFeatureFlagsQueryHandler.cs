using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;

internal sealed class GetFeatureFlagsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetFeatureFlagsQuery, Result<PaginatedList<FeatureFlagDto>>>
{
    public async Task<Result<PaginatedList<FeatureFlagDto>>> Handle(
        GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        // Platform admins (TenantId=null) can view any tenant's resolved flags
        var tenantId = request.TenantId.HasValue && !currentUser.TenantId.HasValue
            ? request.TenantId
            : currentUser.TenantId;
        var query = context.Set<FeatureFlag>().AsNoTracking().AsQueryable();

        if (request.Category.HasValue)
            query = query.Where(f => f.Category == request.Category.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(f => f.Key.Contains(request.Search) || f.Name.Contains(request.Search));

        query = query.OrderBy(f => f.Category).ThenBy(f => f.Key);

        var totalCount = await query.CountAsync(cancellationToken);
        var flags = await query.Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(cancellationToken);

        var overrideMap = new Dictionary<Guid, string>();
        if (tenantId.HasValue && flags.Count > 0)
        {
            var flagIds = flags.Select(f => f.Id).ToList();
            overrideMap = await context.Set<TenantFeatureFlag>().AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value && flagIds.Contains(t.FeatureFlagId))
                .ToDictionaryAsync(t => t.FeatureFlagId, t => t.Value, cancellationToken);
        }

        var dtos = flags.Select(f => f.ToDto(overrideMap.GetValueOrDefault(f.Id))).ToList();
        var paginatedList = PaginatedList<FeatureFlagDto>.Create(dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize);
        return Result.Success(paginatedList);
    }
}
