using System.Diagnostics.Metrics;

namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Central OpenTelemetry meter and instruments for the RAG pipeline.
/// One <see cref="Meter"/> is reused by all components so metrics share a single
/// registration and export path. Tag values are enumerated (see <see cref="RagStageOutcome"/>)
/// to keep Prometheus cardinality bounded.
/// </summary>
internal static class AiRagMetrics
{
    public const string MeterName = "Starter.Module.AI.Rag";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> RetrievalRequests =
        _meter.CreateCounter<long>(
            name: "rag.retrieval.requests",
            unit: "count",
            description: "Number of RAG retrieval calls, tagged by scope.");

    public static readonly Histogram<double> StageDuration =
        _meter.CreateHistogram<double>(
            name: "rag.stage.duration",
            unit: "ms",
            description: "Per-stage latency in milliseconds.");

    public static readonly Counter<long> StageOutcome =
        _meter.CreateCounter<long>(
            name: "rag.stage.outcome",
            unit: "count",
            description: "Per-stage completion outcome (success|timeout|error).");

    public static readonly Counter<long> CacheRequests =
        _meter.CreateCounter<long>(
            name: "rag.cache.requests",
            unit: "count",
            description: "Cache lookups for embed/rewrite/rerank/classify, tagged by hit.");

    public static readonly Histogram<long> FusionCandidates =
        _meter.CreateHistogram<long>(
            name: "rag.fusion.candidates",
            unit: "count",
            description: "Size of fused hybrid-score list before top-K cut.");

    public static readonly Histogram<long> ContextTokens =
        _meter.CreateHistogram<long>(
            name: "rag.context.tokens",
            unit: "tokens",
            description: "Final context token count handed to the chat model.");

    public static readonly Counter<long> ContextTruncated =
        _meter.CreateCounter<long>(
            name: "rag.context.truncated",
            unit: "count",
            description: "Number of retrievals whose context was truncated, tagged by reason.");

    public static readonly Counter<long> DegradedStages =
        _meter.CreateCounter<long>(
            name: "rag.degraded.stages",
            unit: "count",
            description: "One increment per degraded stage per retrieval call.");

    public static readonly Counter<long> RerankReordered =
        _meter.CreateCounter<long>(
            name: "rag.rerank.reordered",
            unit: "count",
            description: "Whether rerank changed the top-K order vs RRF fusion.");

    public static readonly Histogram<long> KeywordHits =
        _meter.CreateHistogram<long>(
            name: "rag.keyword.hits",
            unit: "count",
            description: "Keyword search hit count per query variant, tagged by detected language.");
}
