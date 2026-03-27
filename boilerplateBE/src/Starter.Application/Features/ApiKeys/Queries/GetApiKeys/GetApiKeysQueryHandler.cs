using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeys;

public sealed class GetApiKeysQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetApiKeysQuery, Result<PaginatedList<ApiKeyDto>>>
{
    public async Task<Result<PaginatedList<ApiKeyDto>>> Handle(
        GetApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.Scopes,
                k.ExpiresAt,
                k.LastUsedAt,
                k.IsRevoked,
                k.IsExpired,
                k.TenantId == null,
                k.TenantId,
                null,
                k.CreatedAt,
                k.CreatedBy));

        var result = await PaginatedList<ApiKeyDto>.CreateAsync(
            query, request.PageNumber, request.PageSize);

        return Result.Success(result);
    }
}
