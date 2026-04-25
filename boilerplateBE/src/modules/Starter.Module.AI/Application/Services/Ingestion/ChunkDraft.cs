using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ChunkDraft(
    int Index,
    string Content,
    int TokenCount,
    int? ParentIndex,
    string? SectionTitle,
    int? PageNumber,
    ChunkType ChunkType = ChunkType.Body);
