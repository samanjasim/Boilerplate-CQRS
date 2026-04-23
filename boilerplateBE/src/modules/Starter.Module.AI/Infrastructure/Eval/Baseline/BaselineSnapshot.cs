namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public sealed record BaselineDatasetSnapshot(
    IReadOnlyDictionary<int, double> RecallAtK,
    IReadOnlyDictionary<int, double> PrecisionAtK,
    IReadOnlyDictionary<int, double> NdcgAtK,
    IReadOnlyDictionary<int, double> HitRateAtK,
    double Mrr,
    IReadOnlyDictionary<string, double> StageP95Ms,
    int DegradedStageCount);

public sealed record BaselineSnapshot(
    DateTime GeneratedAt,
    string? GitSha,
    IReadOnlyDictionary<string, BaselineDatasetSnapshot> Datasets);
