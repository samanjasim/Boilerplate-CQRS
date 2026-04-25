using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiDocumentChunk : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public Guid? ParentChunkId { get; private set; }
    public string ChunkLevel { get; private set; } = default!;
    public string Content { get; private set; } = default!;
    public int ChunkIndex { get; private set; }
    public string? SectionTitle { get; private set; }
    public int? PageNumber { get; private set; }
    public int TokenCount { get; private set; }
    public Guid QdrantPointId { get; private set; }
    public ChunkType ChunkType { get; private set; }

    // Denormalized from the parent AiDocument's FileMetadata so keyword-search
    // can push down the ACL predicate without an extra join.
    public Guid FileId { get; private set; }
    public ResourceVisibility Visibility { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    private AiDocumentChunk() { }

    private AiDocumentChunk(
        Guid id,
        Guid documentId,
        Guid? parentChunkId,
        string chunkLevel,
        string content,
        int chunkIndex,
        string? sectionTitle,
        int? pageNumber,
        int tokenCount,
        Guid qdrantPointId,
        ChunkType chunkType,
        Guid fileId,
        ResourceVisibility visibility,
        Guid uploadedByUserId) : base(id)
    {
        DocumentId = documentId;
        ParentChunkId = parentChunkId;
        ChunkLevel = chunkLevel;
        Content = content;
        ChunkIndex = chunkIndex;
        SectionTitle = sectionTitle;
        PageNumber = pageNumber;
        TokenCount = tokenCount;
        QdrantPointId = qdrantPointId;
        ChunkType = chunkType;
        FileId = fileId;
        Visibility = visibility;
        UploadedByUserId = uploadedByUserId;
    }

    public string? NormalizedContent { get; private set; }

    public void SetNormalizedContent(string normalized)
    {
        NormalizedContent = normalized ?? string.Empty;
        ModifiedAt = DateTime.UtcNow;
    }

    public static AiDocumentChunk Create(
        Guid documentId,
        string chunkLevel,
        string content,
        int chunkIndex,
        int tokenCount,
        Guid qdrantPointId,
        Guid? parentChunkId = null,
        string? sectionTitle = null,
        int? pageNumber = null,
        ChunkType chunkType = ChunkType.Body,
        Guid fileId = default,
        ResourceVisibility visibility = ResourceVisibility.Private,
        Guid uploadedByUserId = default)
    {
        return new AiDocumentChunk(
            Guid.NewGuid(),
            documentId,
            parentChunkId,
            chunkLevel.Trim(),
            content,
            chunkIndex,
            sectionTitle?.Trim(),
            pageNumber,
            tokenCount,
            qdrantPointId,
            chunkType,
            fileId,
            visibility,
            uploadedByUserId);
    }
}
