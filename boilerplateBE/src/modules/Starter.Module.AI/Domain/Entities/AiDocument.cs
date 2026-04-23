using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiDocument : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public Guid FileId { get; private set; }
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public int ChunkCount { get; private set; }
    public EmbeddingStatus EmbeddingStatus { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool RequiresOcr { get; private set; }
    public string? OcrProvider { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    private AiDocument() { }

    private AiDocument(
        Guid id,
        Guid? tenantId,
        string name,
        string fileName,
        Guid fileId,
        string contentType,
        long sizeBytes,
        bool requiresOcr,
        string? ocrProvider,
        Guid uploadedByUserId) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        FileName = fileName;
        FileId = fileId;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        EmbeddingStatus = Enums.EmbeddingStatus.Pending;
        RequiresOcr = requiresOcr;
        OcrProvider = ocrProvider;
        UploadedByUserId = uploadedByUserId;
    }

    public static AiDocument Create(
        Guid? tenantId,
        string name,
        string fileName,
        Guid fileId,
        string contentType,
        long sizeBytes,
        Guid uploadedByUserId,
        bool requiresOcr = false,
        string? ocrProvider = null)
    {
        return new AiDocument(
            Guid.NewGuid(),
            tenantId,
            name.Trim(),
            fileName.Trim(),
            fileId,
            contentType.Trim(),
            sizeBytes,
            requiresOcr,
            ocrProvider?.Trim(),
            uploadedByUserId);
    }

    public void MarkProcessing()
    {
        EmbeddingStatus = Enums.EmbeddingStatus.Processing;
        ErrorMessage = null;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(int chunkCount)
    {
        EmbeddingStatus = Enums.EmbeddingStatus.Completed;
        ChunkCount = chunkCount;
        ProcessedAt = DateTime.UtcNow;
        ErrorMessage = null;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        EmbeddingStatus = Enums.EmbeddingStatus.Failed;
        ErrorMessage = errorMessage;
        ModifiedAt = DateTime.UtcNow;
    }

    public void ResetForReprocessing()
    {
        EmbeddingStatus = Enums.EmbeddingStatus.Pending;
        ChunkCount = 0;
        ProcessedAt = null;
        ErrorMessage = null;
        ModifiedAt = DateTime.UtcNow;
    }

    public string? ContentHash { get; private set; }

    public void SetContentHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64 || !IsHex(hash))
            throw new ArgumentException("ContentHash must be a 64-char hex SHA-256 string.", nameof(hash));
        ContentHash = hash.ToLowerInvariant();
        ModifiedAt = DateTime.UtcNow;
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'))
                continue;
            return false;
        }
        return true;
    }
}
