using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Starter.Application.Common.Constants;
using Starter.Infrastructure.Settings;

namespace Starter.Infrastructure.Services;

public sealed class FileService(
    IApplicationDbContext context,
    IStorageService storageService,
    ICurrentUserService currentUserService,
    ISettingsProvider settingsProvider,
    IOptions<StorageSettings> settings) : IFileService
{
    private readonly StorageSettings _settings = settings.Value;

    public async Task<FileMetadata> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        long size,
        FileCategory category,
        Guid? entityId = null,
        string? entityType = null,
        string? description = null,
        string[]? tags = null,
        ResourceVisibility visibility = ResourceVisibility.Private,
        CancellationToken ct = default)
    {
        var tenantId = currentUserService.TenantId;
        var userId = currentUserService.UserId
                     ?? throw new InvalidOperationException("User must be authenticated to upload files.");

        var folder = tenantId?.ToString() ?? "platform";
        var extension = Path.GetExtension(fileName);
        var key = $"{folder}/{category.ToString().ToLowerInvariant()}/{Guid.NewGuid()}{extension}";

        await storageService.UploadAsync(stream, key, contentType, ct);

        var metadata = FileMetadata.Create(
            fileName: fileName,
            storageKey: key,
            contentType: contentType,
            size: size,
            category: category,
            uploadedBy: userId,
            tenantId: tenantId,
            tags: tags is { Length: > 0 } ? string.Join(",", tags) : null,
            visibility: visibility,
            description: description,
            entityType: entityType,
            entityId: entityId);

        context.Set<FileMetadata>().Add(metadata);
        await context.SaveChangesAsync(ct);

        return metadata;
    }

    public async Task<string> GetUrlAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.Set<FileMetadata>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new InvalidOperationException($"File with ID {fileId} not found.");

        if (metadata.Visibility == ResourceVisibility.Public)
            return await storageService.GetPublicUrlAsync(metadata.StorageKey, ct);

        return await storageService.GetSignedUrlAsync(
            metadata.StorageKey,
            TimeSpan.FromMinutes(_settings.SignedUrlExpirationMinutes),
            ct);
    }

    public async Task DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new InvalidOperationException($"File with ID {fileId} not found.");

        await storageService.DeleteAsync(metadata.StorageKey, ct);

        context.Set<FileMetadata>().Remove(metadata);
        await context.SaveChangesAsync(ct);
    }

    public async Task<FileMetadata> UploadTemporaryAsync(
        Stream stream,
        string fileName,
        string contentType,
        long size,
        FileCategory category,
        string? description = null,
        string[]? tags = null,
        CancellationToken ct = default)
    {
        var tenantId = currentUserService.TenantId;
        var userId = currentUserService.UserId
                     ?? throw new InvalidOperationException("User must be authenticated to upload files.");

        var ttlMinutes = await settingsProvider.GetIntAsync(FileSettings.TempFileTtlMinutesKey, FileSettings.TempFileTtlMinutesDefault, ct);

        var folder = tenantId?.ToString() ?? "platform";
        var extension = Path.GetExtension(fileName);
        var key = $"{folder}/{category.ToString().ToLowerInvariant()}/{Guid.NewGuid()}{extension}";

        await storageService.UploadAsync(stream, key, contentType, ct);

        var metadata = FileMetadata.Create(
            fileName: fileName,
            storageKey: key,
            contentType: contentType,
            size: size,
            category: category,
            uploadedBy: userId,
            tenantId: tenantId,
            tags: tags is { Length: > 0 } ? string.Join(",", tags) : null,
            description: description,
            status: FileStatus.Temporary,
            origin: FileOrigin.ProcessUpload,
            expiresAt: DateTime.UtcNow.AddMinutes(ttlMinutes));

        context.Set<FileMetadata>().Add(metadata);
        await context.SaveChangesAsync(ct);

        return metadata;
    }

    public async Task<FileMetadata> CreateSystemFileAsync(
        Stream stream,
        string fileName,
        string contentType,
        long size,
        FileCategory category,
        Guid? tenantId = null,
        string? description = null,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        var folder = tenantId?.ToString() ?? "platform";
        var extension = Path.GetExtension(fileName);
        var key = $"{folder}/{category.ToString().ToLowerInvariant()}/{Guid.NewGuid()}{extension}";

        await storageService.UploadAsync(stream, key, contentType, ct);

        var metadata = FileMetadata.Create(
            fileName: fileName,
            storageKey: key,
            contentType: contentType,
            size: size,
            category: category,
            uploadedBy: Guid.Empty,
            tenantId: tenantId,
            description: description,
            status: FileStatus.Permanent,
            origin: FileOrigin.SystemGenerated,
            expiresAt: expiresAt);

        context.Set<FileMetadata>().Add(metadata);
        await context.SaveChangesAsync(ct);

        return metadata;
    }

    public async Task AttachToEntityAsync(Guid fileId, Guid entityId, string entityType, CancellationToken ct = default)
    {
        var metadata = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new InvalidOperationException($"File with ID {fileId} not found.");

        metadata.MarkPermanent();
        metadata.LinkToEntity(entityId, entityType);
        await context.SaveChangesAsync(ct);
    }

    public async Task DetachFromEntityAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new InvalidOperationException($"File with ID {fileId} not found.");

        metadata.Unlink();
        await context.SaveChangesAsync(ct);
    }

    public async Task ReplaceEntityFileAsync(Guid? oldFileId, Guid newFileId, Guid entityId, string entityType, CancellationToken ct = default)
    {
        if (oldFileId.HasValue && oldFileId.Value == newFileId) return;

        if (oldFileId.HasValue)
        {
            var oldFile = await context.Set<FileMetadata>()
                .FirstOrDefaultAsync(f => f.Id == oldFileId.Value, ct);

            if (oldFile is not null)
            {
                if (oldFile.Origin == FileOrigin.ProcessUpload)
                {
                    await storageService.DeleteAsync(oldFile.StorageKey, ct);
                    context.Set<FileMetadata>().Remove(oldFile);
                }
                else
                {
                    oldFile.Unlink();
                }
            }
        }

        var newFile = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == newFileId, ct)
            ?? throw new InvalidOperationException($"File with ID {newFileId} not found.");

        newFile.MarkPermanent();
        newFile.LinkToEntity(entityId, entityType);

        await context.SaveChangesAsync(ct);
    }

    public async Task<FileMetadata> CreateManagedFileAsync(ManagedFileUpload upload, CancellationToken ct = default)
    {
        var tenantId = currentUserService.TenantId;
        var userId = currentUserService.UserId
                     ?? throw new InvalidOperationException("User must be authenticated to upload files.");

        var fileId = Guid.NewGuid();
        var key = BuildStorageKey(tenantId, upload.Category, fileId, upload.FileName);

        await storageService.UploadAsync(upload.Stream, key, upload.ContentType, ct);

        var metadata = FileMetadata.Create(
            fileName: upload.FileName,
            storageKey: key,
            contentType: upload.ContentType,
            size: upload.Size,
            category: upload.Category,
            uploadedBy: userId,
            tenantId: tenantId,
            tags: upload.Tags is { Length: > 0 } ? string.Join(",", upload.Tags) : null,
            visibility: upload.Visibility,
            description: upload.Description,
            entityType: upload.EntityType,
            entityId: upload.EntityId,
            status: FileStatus.Permanent,
            origin: upload.Origin);

        context.Set<FileMetadata>().Add(metadata);
        await context.SaveChangesAsync(ct);

        return metadata;
    }

    public async Task DeleteManagedFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.Set<FileMetadata>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);
        if (metadata is null) return;

        await storageService.DeleteAsync(metadata.StorageKey, ct);
        context.Set<FileMetadata>().Remove(metadata);
        await context.SaveChangesAsync(ct);
    }

    public async Task<FileDownloadResult?> ResolveDownloadAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.Set<FileMetadata>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);
        if (metadata is null) return null;

        var stream = await storageService.DownloadAsync(metadata.StorageKey, ct);
        return new FileDownloadResult(stream, metadata.ContentType, metadata.FileName);
    }

    private static string BuildStorageKey(Guid? tenantId, FileCategory category, Guid fileId, string safeName)
    {
        var folder = tenantId?.ToString() ?? "platform";
        var extension = Path.GetExtension(safeName);
        return $"{folder}/{category.ToString().ToLowerInvariant()}/{fileId}{extension}";
    }
}
