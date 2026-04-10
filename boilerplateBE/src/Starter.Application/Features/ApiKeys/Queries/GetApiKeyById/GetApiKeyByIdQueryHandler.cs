using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeyById;

public sealed class GetApiKeyByIdQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetApiKeyByIdQuery, Result<ApiKeyDto>>
{
    public async Task<Result<ApiKeyDto>> Handle(GetApiKeyByIdQuery request, CancellationToken cancellationToken)
    {
        var isPlatformAdmin = !currentUserService.TenantId.HasValue;

        if (isPlatformAdmin)
        {
            // Platform admin: see all keys, populate TenantName
            var result = await (
                from k in dbContext.Set<ApiKey>().IgnoreQueryFilters().AsNoTracking()
                join t in dbContext.Tenants.IgnoreQueryFilters() on k.TenantId equals t.Id into tj
                from tenant in tj.DefaultIfEmpty()
                where k.Id == request.Id
                select new ApiKeyDto(
                    k.Id, k.Name, k.KeyPrefix, k.Scopes,
                    k.ExpiresAt, k.LastUsedAt, k.IsRevoked, k.IsExpired,
                    k.TenantId == null, k.TenantId,
                    tenant != null ? tenant.Name : null,
                    k.CreatedAt, k.CreatedBy)
            ).FirstOrDefaultAsync(cancellationToken);

            return result is not null
                ? Result.Success(result)
                : Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
        }
        else
        {
            // Tenant user: global filter applies
            var apiKey = await dbContext.Set<ApiKey>()
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

            return apiKey is not null
                ? Result.Success(apiKey.ToDto())
                : Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
        }
    }
}
