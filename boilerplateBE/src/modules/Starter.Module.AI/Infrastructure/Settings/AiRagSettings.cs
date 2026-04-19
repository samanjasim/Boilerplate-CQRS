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
    public bool EnableReranking { get; init; } = false;       // 4b-2
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;
    public double OcrFallbackMinCharsPerPage { get; init; } = 40;
    public double PageFailureThreshold { get; init; } = 0.25;

    // From Plan 4b
    public int MaxContextTokens { get; init; } = 4000;
    public bool IncludeParentContext { get; init; } = true;
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

    // New in Plan 4b-1 — Arabic / FTS language
    public string FtsLanguage { get; init; } = "simple";
    public bool ApplyArabicNormalization { get; init; } = true;
    public bool NormalizeTaMarbuta { get; init; } = true;
    public bool NormalizeArabicDigits { get; init; } = true;
}
