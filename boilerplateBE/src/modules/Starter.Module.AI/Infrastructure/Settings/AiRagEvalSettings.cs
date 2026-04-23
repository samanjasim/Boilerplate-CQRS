namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagEvalSettings
{
    public const string SectionName = "AI:Rag:Eval";

    public bool Enabled { get; init; } = false;
    public string FixtureDirectory { get; init; } = "ai-eval-fixtures";
    public string BaselineFile { get; init; } = "ai-eval-fixtures/rag-eval-baseline.json";
    public double MetricTolerance { get; init; } = 0.05;
    public double LatencyTolerance { get; init; } = 0.20;
    public string? JudgeModel { get; init; } = null;
    public int JudgeTimeoutMs { get; init; } = 30_000;
    public int WarmupQueries { get; init; } = 2;
    public int[] KValues { get; init; } = new[] { 5, 10, 20 };
}
