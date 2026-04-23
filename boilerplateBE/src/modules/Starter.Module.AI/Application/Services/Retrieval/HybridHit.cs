namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record HybridHit(Guid ChunkId, decimal SemanticScore, decimal KeywordScore, decimal HybridScore);
