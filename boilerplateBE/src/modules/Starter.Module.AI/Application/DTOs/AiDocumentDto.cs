namespace Starter.Module.AI.Application.DTOs;

public sealed record AiDocumentDto(
    Guid Id,
    string Name,
    string FileName,
    string ContentType,
    long SizeBytes,
    int ChunkCount,
    string EmbeddingStatus,
    string? ErrorMessage,
    bool RequiresOcr,
    DateTime? ProcessedAt,
    DateTime CreatedAt,
    Guid UploadedByUserId);
