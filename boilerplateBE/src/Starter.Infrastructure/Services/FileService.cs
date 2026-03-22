using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Infrastructure.Settings;

namespace Starter.Infrastructure.Services;

public sealed class FileService(
    IApplicationDbContext context,
    IStorageService storageService,
    ICurrentUserService currentUserService,
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
        bool isPublic = false,
        CancellationToken ct = default)
    {
        var tenantId = currentUserService.TenantId;
        var userId = currentUserService.UserId
                     ?? throw new InvalidOperationException("User must be authenticated to upload files.");

        var folder = tenantId?.ToString() ?? "platform";
        var extension = Path.GetExtension(fileName);
        var key = $"{folder}/{category.ToString().ToLowerInvariant()}/{Guid.NewGuid()}{extension}";

        await storageService.UploadAsync(stream, key, contentType, ct);

        var metadata = new FileMetadata
        {
            FileName = fileName,
            StorageKey = key,
            ContentType = contentType,
            Size = size,
            Category = category,
            Tags = tags is { Length: > 0 } ? string.Join(",", tags) : null,
            TenantId = tenantId,
            UploadedBy = userId,
            IsPublic = isPublic,
            Description = description,
            EntityType = entityType,
            EntityId = entityId
        };

        context.FileMetadata.Add(metadata);
        await context.SaveChangesAsync(ct);

        return metadata;
    }

    public async Task<string> GetUrlAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.FileMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new InvalidOperationException($"File with ID {fileId} not found.");

        if (metadata.IsPublic)
            return await storageService.GetPublicUrlAsync(metadata.StorageKey, ct);

        return await storageService.GetSignedUrlAsync(
            metadata.StorageKey,
            TimeSpan.FromMinutes(_settings.SignedUrlExpirationMinutes),
            ct);
    }

    public async Task DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        var metadata = await context.FileMetadata
            .FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new InvalidOperationException($"File with ID {fileId} not found.");

        await storageService.DeleteAsync(metadata.StorageKey, ct);

        context.FileMetadata.Remove(metadata);
        await context.SaveChangesAsync(ct);
    }
}
