using Starter.Domain.ApiKeys.Entities;

namespace Starter.Application.Features.ApiKeys.DTOs;

public static class ApiKeyMapper
{
    public static ApiKeyDto ToDto(this ApiKey entity, string? tenantName = null)
    {
        return new ApiKeyDto(
            entity.Id,
            entity.Name,
            entity.KeyPrefix,
            entity.Scopes,
            entity.ExpiresAt,
            entity.LastUsedAt,
            entity.IsRevoked,
            entity.IsExpired,
            entity.IsPlatformKey,
            entity.TenantId,
            tenantName,
            entity.CreatedAt,
            entity.CreatedBy);
    }
}
