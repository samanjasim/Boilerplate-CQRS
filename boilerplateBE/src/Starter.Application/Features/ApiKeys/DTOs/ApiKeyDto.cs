namespace Starter.Application.Features.ApiKeys.DTOs;

public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    List<string> Scopes,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked,
    bool IsExpired,
    bool IsPlatformKey,
    Guid? TenantId,
    string? TenantName,
    DateTime CreatedAt,
    Guid? CreatedBy);
