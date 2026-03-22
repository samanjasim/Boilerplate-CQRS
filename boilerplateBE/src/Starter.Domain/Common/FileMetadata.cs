using Starter.Domain.Common.Enums;

namespace Starter.Domain.Common;

public class FileMetadata : BaseAuditableEntity
{
    public string FileName { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Size { get; set; }
    public FileCategory Category { get; set; }
    public string? Tags { get; set; }
    public Guid? TenantId { get; set; }
    public Guid UploadedBy { get; set; }
    public bool IsPublic { get; set; }
    public string? Description { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
}
