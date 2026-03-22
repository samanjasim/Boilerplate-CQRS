using Starter.Domain.Common.Enums;

namespace Starter.Application.Features.Files;

public sealed record FileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    FileCategory Category,
    string? Tags,
    Guid? TenantId,
    Guid UploadedBy,
    bool IsPublic,
    string? Description,
    string? EntityType,
    Guid? EntityId,
    DateTime CreatedAt,
    string? Url = null);
