namespace Starter.Module.AI.Application.DTOs;

public sealed record AiDocumentDetailDto(
    AiDocumentDto Document,
    IReadOnlyList<AiDocumentChunkPreviewDto> ChunkPreviews);
