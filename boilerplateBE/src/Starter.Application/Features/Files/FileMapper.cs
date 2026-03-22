using Starter.Domain.Common;

namespace Starter.Application.Features.Files;

public static class FileMapper
{
    public static FileDto ToDto(this FileMetadata file, string? url = null) =>
        new(
            file.Id,
            file.FileName,
            file.ContentType,
            file.Size,
            file.Category,
            file.Tags,
            file.TenantId,
            file.UploadedBy,
            file.IsPublic,
            file.Description,
            file.EntityType,
            file.EntityId,
            file.CreatedAt,
            url);
}
