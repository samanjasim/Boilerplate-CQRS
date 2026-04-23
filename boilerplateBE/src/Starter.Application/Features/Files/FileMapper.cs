using Starter.Domain.Common;

namespace Starter.Application.Features.Files;

public static class FileMapper
{
    public static FileDto ToDto(this FileMetadata file, string? url = null, string? uploadedByName = null) =>
        new(
            file.Id,
            file.FileName,
            file.ContentType,
            file.Size,
            file.Category.ToString(),
            file.Tags,
            file.TenantId,
            file.UploadedBy,
            uploadedByName,
            file.Visibility,
            file.Description,
            file.EntityType,
            file.EntityId,
            file.CreatedAt,
            url,
            file.Status.ToString(),
            file.Origin.ToString(),
            file.ExpiresAt);
}
