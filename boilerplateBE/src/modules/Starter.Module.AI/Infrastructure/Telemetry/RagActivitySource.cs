using System.Diagnostics;

namespace Starter.Module.AI.Infrastructure.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/> for RAG pipeline stages. Register with
/// OpenTelemetry via <c>.AddSource(RagActivitySource.Name)</c>. Tag names are
/// defined in <see cref="RagTracingTags"/> so dashboards can query them consistently.
/// </summary>
public static class RagActivitySource
{
    public const string Name = "Starter.Module.AI";
    public static readonly ActivitySource Source = new(Name);
}

/// <summary>
/// Canonical tag keys for RAG pipeline spans. Defined in one place so the spec
/// (plan-4b-2 §5) and Grafana dashboards can't drift apart.
/// </summary>
public static class RagTracingTags
{
    // Retrieval-level
    public const string RetrieveVariantsUsed = "rag.retrieve.variants_used";
    public const string RetrieveDegradedStages = "rag.retrieve.degraded_stages";
    public const string RetrievePoolSize = "rag.retrieve.pool_size";
    public const string RetrieveTopK = "rag.retrieve.top_k";
    public const string RetrieveTruncated = "rag.retrieve.truncated";

    // Classify
    public const string ClassifyType = "rag.classify.type";

    // Query rewrite
    public const string RewriteVariantsUsed = "rag.rewrite.variants_used";

    // Rerank
    public const string RerankStrategyRequested = "rag.rerank.strategy_requested";
    public const string RerankStrategyUsed = "rag.rerank.strategy_used";
    public const string RerankFellBack = "rag.rerank.fell_back";
    public const string RerankCacheHits = "rag.rerank.cache_hits";
    public const string RerankLatencyMs = "rag.rerank.latency_ms";
    public const string RerankUnusedRatio = "rag.rerank.unused_ratio";

    // Neighbor
    public const string NeighborSiblingsReturned = "rag.neighbor.siblings_returned";
}
