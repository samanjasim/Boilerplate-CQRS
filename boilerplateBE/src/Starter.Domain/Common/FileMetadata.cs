using Starter.Domain.Common.Enums;

namespace Starter.Domain.Common;

public sealed class FileMetadata : BaseAuditableEntity
{
    public string FileName { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long Size { get; private set; }
    public FileCategory Category { get; private set; }
    public string? Tags { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid UploadedBy { get; private set; }
    public bool IsPublic { get; private set; }
    public string? Description { get; private set; }
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public FileStatus Status { get; private set; }
    public FileOrigin Origin { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private FileMetadata() { }

    public static FileMetadata Create(
        string fileName,
        string storageKey,
        string contentType,
        long size,
        FileCategory category,
        Guid uploadedBy,
        Guid? tenantId = null,
        string? tags = null,
        bool isPublic = false,
        string? description = null,
        string? entityType = null,
        Guid? entityId = null,
        FileStatus status = FileStatus.Permanent,
        FileOrigin origin = FileOrigin.UserUpload,
        DateTime? expiresAt = null)
    {
        return new FileMetadata(Guid.NewGuid())
        {
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = contentType,
            Size = size,
            Category = category,
            UploadedBy = uploadedBy,
            TenantId = tenantId,
            Tags = tags,
            IsPublic = isPublic,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            Status = status,
            Origin = origin,
            ExpiresAt = expiresAt
        };
    }

    public void UpdateMetadata(string? description, FileCategory? category, string? tags)
    {
        if (description is not null) Description = description;
        if (category is not null) Category = category.Value;
        if (tags is not null) Tags = tags;
    }

    public void MarkPermanent()
    {
        Status = FileStatus.Permanent;
        ExpiresAt = null;
    }

    public void Unlink()
    {
        EntityType = null;
        EntityId = null;
    }

    public void LinkToEntity(Guid entityId, string entityType)
    {
        EntityId = entityId;
        EntityType = entityType;
    }

    private FileMetadata(Guid id) : base(id) { }
}
