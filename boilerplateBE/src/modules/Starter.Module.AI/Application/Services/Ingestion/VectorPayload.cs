using Starter.Domain.Common.Access.Enums;
using Starter.Abstractions.Ai;
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
    Guid FileId,
    ResourceVisibility Visibility,
    Guid UploadedByUserId,
    ChunkType ChunkType = ChunkType.Body);
