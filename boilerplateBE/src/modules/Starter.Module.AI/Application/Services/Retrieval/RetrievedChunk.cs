namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    string ChunkLevel,
    decimal SemanticScore,
    decimal KeywordScore,
    decimal HybridScore,
    Guid? ParentChunkId,
    int ChunkIndex);
