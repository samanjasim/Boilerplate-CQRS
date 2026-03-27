namespace Starter.Application.Features.ApiKeys.DTOs;

public sealed record CreateApiKeyResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    string FullKey,
    List<string> Scopes,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
