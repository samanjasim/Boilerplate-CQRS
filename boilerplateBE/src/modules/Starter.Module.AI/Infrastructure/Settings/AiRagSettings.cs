using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagSettings
{
    public const string SectionName = "AI:Rag";

    // From Plan 4a
    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 50;
    public int ParentChunkSize { get; init; } = 1536;
    public int TopK { get; init; } = 5;
    public int RetrievalTopK { get; init; } = 20;
    public bool EnableQueryExpansion { get; init; } = false;  // 4b-2
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;
    public double OcrFallbackMinCharsPerPage { get; init; } = 40;
    public double PageFailureThreshold { get; init; } = 0.25;

    // From Plan 4b
    public int MaxContextTokens { get; init; } = 4000;
    public bool IncludeParentContext { get; init; } = true;

    // RRF-based minimum score gate. Fusion scores are computed as
    // Σ weight/(RrfK + rank + 1); with default RrfK=60 the top-1 score is ~0.0164,
    // rank 20 is ~0.0125. Pre-4b-1 min-max values (e.g. 0.3) would reject every
    // hit under RRF and turn RAG into a silent no-op. Keep at 0.0 unless you have
    // measured scores for your corpus.
    public decimal MinHybridScore { get; init; } = 0.0m;

    // New in Plan 4b-1 — RRF fusion (replaces HybridSearchWeight)
    public decimal VectorWeight { get; init; } = 1.0m;
    public decimal KeywordWeight { get; init; } = 1.0m;
    public int RrfK { get; init; } = 60;

    // New in Plan 4b-1 — embedding cache
    public int EmbeddingCacheTtlSeconds { get; init; } = 3600;

    // New in Plan 4b-1 — per-stage timeouts
    public int StageTimeoutEmbedMs { get; init; } = 5_000;
    public int StageTimeoutVectorMs { get; init; } = 5_000;
    public int StageTimeoutKeywordMs { get; init; } = 3_000;

    // New in Plan 4b-1 — Arabic / FTS language.
    // WARNING: This must match the regconfig literal in the `content_tsv` generated
    // column defined in AiDocumentChunkConfiguration. Changing this at runtime without
    // a coordinated migration (rewriting the generated column with the new dictionary)
    // will silently produce zero hits because the query dictionary will not match the
    // tokens in the index. Safe values today: "simple". Do not change without an ops plan.
    public string FtsLanguage { get; init; } = "simple";
    public bool ApplyArabicNormalization { get; init; } = true;
    public bool NormalizeTaMarbuta { get; init; } = true;
    public bool NormalizeArabicDigits { get; init; } = true;

    // ---- New in Plan 4b-2 — Query rewriter ----
    public int QueryRewriteMaxVariants { get; init; } = 3;
    public int QueryRewriteCacheTtlSeconds { get; init; } = 1800;
    public int StageTimeoutQueryRewriteMs { get; init; } = 4_000;
    public string? RewriterModel { get; init; } = null;

    // ---- New in Plan 4b-2 — Reranker (hybrid) ----
    // Replaces the legacy EnableReranking bool (which was a no-op in 4b-1).
    // Mapping when migrating appsettings: true → Auto, false → Off.
    public RerankStrategy RerankStrategy { get; init; } = RerankStrategy.Auto;
    public int ListwisePoolMultiplier { get; init; } = 3;
    public int PointwisePoolMultiplier { get; init; } = 2;
    public int PointwiseMaxParallelism { get; init; } = 5;
    public decimal MinPointwiseScore { get; init; } = 0.3m;
    public double PointwiseMaxFailureRatio { get; init; } = 0.25;
    public int RerankCacheTtlSeconds { get; init; } = 1800;
    public string? RerankerModel { get; init; } = null;
    public int StageTimeoutRerankMs { get; init; } = 8_000;

    // ---- New in Plan 4b-2 — Question classifier ----
    public int QuestionCacheTtlSeconds { get; init; } = 1800;
    public int StageTimeoutClassifyMs { get; init; } = 2_000;
    public string? ClassifierModel { get; init; } = null;

    // ---- New in Plan 4b-2 — Neighbor expansion ----
    public int NeighborWindowSize { get; init; } = 0;
    public int StageTimeoutNeighborMs { get; init; } = 3_000;

    // ---- New in Plan 4b-3 — Structural chunking ----
    // When false, ChunkerRouter always delegates to the heuristic HierarchicalDocumentChunker,
    // regardless of ContentType. Use for A/B experiments or corpus-specific regressions.
    public bool EnableStructuralChunking { get; init; } = true;
    // When false, ProcessDocumentConsumer will not prepend the heading breadcrumb to
    // NormalizedContent. Breadcrumbs still flow through SectionTitle and into prompts;
    // the toggle only controls whether they participate in the Postgres FTS index.
    public bool IncludeBreadcrumbInFts { get; init; } = true;

    /// <summary>
    /// Scalar applied to an anchor's HybridScore when attributing a score to its
    /// siblings. Siblings do not earn their own retrieval score — they inherit a
    /// damped fraction of the nearest anchor's score so downstream consumers can
    /// still rank or gate them without pretending they were independently retrieved.
    /// Range [0, 1]; default 0.5 (half the anchor's score).
    /// </summary>
    public decimal NeighborScoreWeight { get; init; } = 0.5m;
}
