using Starter.Domain.Common;

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
        Guid qdrantPointId) : base(id)
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
        int? pageNumber = null)
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
            qdrantPointId);
    }
}
