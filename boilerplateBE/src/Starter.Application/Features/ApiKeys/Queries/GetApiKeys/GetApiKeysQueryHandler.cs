using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Entities;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeys;

public sealed class GetApiKeysQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetApiKeysQuery, Result<PaginatedList<ApiKeyDto>>>
{
    public async Task<Result<PaginatedList<ApiKeyDto>>> Handle(
        GetApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        var isPlatformAdmin = !currentUserService.TenantId.HasValue;

        IQueryable<ApiKeyDto> query;

        if (isPlatformAdmin)
        {
            // Platform admin: use IgnoreQueryFilters, filter by keyType
            var baseQuery = dbContext.Set<ApiKey>()
                .IgnoreQueryFilters()
                .AsNoTracking();

            // Apply keyType filter
            var keyType = request.KeyType?.ToLowerInvariant();
            if (keyType == "platform")
                baseQuery = baseQuery.Where(k => k.TenantId == null);
            else if (keyType == "tenant")
                baseQuery = baseQuery.Where(k => k.TenantId != null);
            // "all" or null: no additional filter (default to platform for safety)
            else if (keyType is null or "")
                baseQuery = baseQuery.Where(k => k.TenantId == null);

            // Optional tenant filter
            if (request.TenantId.HasValue)
                baseQuery = baseQuery.Where(k => k.TenantId == request.TenantId.Value);

            // Left-join tenants for TenantName
            query = from k in baseQuery
                    join t in dbContext.Tenants.IgnoreQueryFilters() on k.TenantId equals t.Id into tj
                    from tenant in tj.DefaultIfEmpty()
                    orderby k.CreatedAt descending
                    select new ApiKeyDto(
                        k.Id, k.Name, k.KeyPrefix, k.Scopes,
                        k.ExpiresAt, k.LastUsedAt, k.IsRevoked, k.IsExpired,
                        k.TenantId == null, k.TenantId,
                        tenant != null ? tenant.Name : null,
                        k.CreatedAt, k.CreatedBy);
        }
        else
        {
            // Tenant user: global filter applies, no join needed
            query = dbContext.Set<ApiKey>()
                .AsNoTracking()
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyDto(
                    k.Id, k.Name, k.KeyPrefix, k.Scopes,
                    k.ExpiresAt, k.LastUsedAt, k.IsRevoked, k.IsExpired,
                    false, k.TenantId, null,
                    k.CreatedAt, k.CreatedBy));
        }

        var result = await PaginatedList<ApiKeyDto>.CreateAsync(
            query, request.PageNumber, request.PageSize);

        return Result.Success(result);
    }
}
