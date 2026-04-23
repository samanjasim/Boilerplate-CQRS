using Starter.Domain.Common.Access.Enums;

namespace Starter.Application.Features.Files;

public sealed record FileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Category,
    string? Tags,
    Guid? TenantId,
    Guid UploadedBy,
    string? UploadedByName,
    ResourceVisibility Visibility,
    string? Description,
    string? EntityType,
    Guid? EntityId,
    DateTime CreatedAt,
    string? Url = null,
    string? Status = null,
    string? Origin = null,
    DateTime? ExpiresAt = null);
