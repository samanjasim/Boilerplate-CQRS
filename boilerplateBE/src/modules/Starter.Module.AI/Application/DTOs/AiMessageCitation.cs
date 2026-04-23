namespace Starter.Module.AI.Application.DTOs;

public sealed record AiMessageCitation(
    int Marker,
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string? SectionTitle,
    int? PageNumber,
    decimal Score);
