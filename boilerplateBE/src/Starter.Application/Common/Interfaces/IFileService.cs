using Starter.Domain.Common;
using Starter.Domain.Common.Enums;

namespace Starter.Application.Common.Interfaces;

public interface IFileService
{
    Task<FileMetadata> UploadAsync(Stream stream, string fileName, string contentType, long size, FileCategory category, Guid? entityId = null, string? entityType = null, string? description = null, string[]? tags = null, bool isPublic = false, CancellationToken ct = default);
    Task<string> GetUrlAsync(Guid fileId, CancellationToken ct = default);
    Task DeleteAsync(Guid fileId, CancellationToken ct = default);

    Task<FileMetadata> UploadTemporaryAsync(Stream stream, string fileName, string contentType, long size, FileCategory category, string? description = null, string[]? tags = null, CancellationToken ct = default);

    Task<FileMetadata> CreateSystemFileAsync(Stream stream, string fileName, string contentType, long size, FileCategory category, Guid? tenantId = null, string? description = null, DateTime? expiresAt = null, CancellationToken ct = default);

    Task AttachToEntityAsync(Guid fileId, Guid entityId, string entityType, CancellationToken ct = default);

    Task DetachFromEntityAsync(Guid fileId, CancellationToken ct = default);

    Task ReplaceEntityFileAsync(Guid? oldFileId, Guid newFileId, Guid entityId, string entityType, CancellationToken ct = default);
}
