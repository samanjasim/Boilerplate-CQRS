using Starter.Module.AI.Infrastructure.Retrieval;

namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RerankResult(
    IReadOnlyList<HybridHit> Ordered,
    RerankStrategy StrategyRequested,
    RerankStrategy StrategyUsed,
    int CandidatesIn,
    int CandidatesScored,
    int CacheHits,
    long LatencyMs,
    int TokensIn,
    int TokensOut,
    double UnusedRatio);
