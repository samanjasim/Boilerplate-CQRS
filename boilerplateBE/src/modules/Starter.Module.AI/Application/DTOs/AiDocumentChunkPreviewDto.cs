namespace Starter.Module.AI.Application.DTOs;

public sealed record AiDocumentChunkPreviewDto(
    Guid Id,
    string ChunkLevel,
    int ChunkIndex,
    int TokenCount,
    int? PageNumber,
    string? SectionTitle,
    string ContentPreview);
