using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record VectorPayload(
    Guid DocumentId,
    string DocumentName,
    string ChunkLevel,
    int ChunkIndex,
    string? SectionTitle,
    int? PageNumber,
    Guid? ParentChunkId,
    Guid TenantId,
    ChunkType ChunkType = ChunkType.Body);
