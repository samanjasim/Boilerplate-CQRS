namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record HierarchicalChunks(
    IReadOnlyList<ChunkDraft> Parents,
    IReadOnlyList<ChunkDraft> Children);
