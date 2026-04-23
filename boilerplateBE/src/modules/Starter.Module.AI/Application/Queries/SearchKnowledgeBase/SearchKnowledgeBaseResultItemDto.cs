namespace Starter.Module.AI.Application.Queries.SearchKnowledgeBase;

public sealed record SearchKnowledgeBaseResultItemDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    string ChunkLevel,
    decimal? HybridScore,
    decimal? SemanticScore,
    decimal? KeywordScore,
    Guid? ParentChunkId);
