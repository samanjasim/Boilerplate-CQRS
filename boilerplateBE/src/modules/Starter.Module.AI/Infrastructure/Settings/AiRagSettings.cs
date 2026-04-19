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
    public double HybridSearchWeight { get; init; } = 0.7;
    public bool EnableQueryExpansion { get; init; } = false;  // flipped 4a → 4b: wired in 4b-2
    public bool EnableReranking { get; init; } = false;       // flipped 4a → 4b: wired in 4b-2
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;
    public double OcrFallbackMinCharsPerPage { get; init; } = 40;
    public double PageFailureThreshold { get; init; } = 0.25;

    // New in Plan 4b
    public int MaxContextTokens { get; init; } = 4000;
    public bool IncludeParentContext { get; init; } = true;
    public decimal MinHybridScore { get; init; } = 0.0m;
    public string FtsLanguage { get; init; } = "english";
}
