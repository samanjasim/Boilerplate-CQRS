namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record VectorSearchHit(Guid ChunkId, decimal Score);
