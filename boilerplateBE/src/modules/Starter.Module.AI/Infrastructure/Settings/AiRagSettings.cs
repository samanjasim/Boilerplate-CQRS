namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagSettings
{
    public const string SectionName = "AI:Rag";

    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 50;
    public int ParentChunkSize { get; init; } = 1536;
    public int TopK { get; init; } = 5;                 // consumed in Plan 4b
    public int RetrievalTopK { get; init; } = 20;       // consumed in Plan 4b
    public double HybridSearchWeight { get; init; } = 0.7;  // consumed in Plan 4b
    public bool EnableQueryExpansion { get; init; } = true; // consumed in Plan 4b
    public bool EnableReranking { get; init; } = true;      // consumed in Plan 4b
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024; // 25 MB
    public double OcrFallbackMinCharsPerPage { get; init; } = 40; // below this → OCR the page
    public double PageFailureThreshold { get; init; } = 0.25;     // fail whole doc if > 25% of pages error
}
