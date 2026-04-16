namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ChunkDraft(
    int Index,
    string Content,
    int TokenCount,
    int? ParentIndex,
    string? SectionTitle,
    int? PageNumber);
