namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record MetricBucket(
    IReadOnlyDictionary<int, double> RecallAtK,
    IReadOnlyDictionary<int, double> PrecisionAtK,
    IReadOnlyDictionary<int, double> NdcgAtK,
    IReadOnlyDictionary<int, double> HitRateAtK,
    double Mrr);

public sealed record EvalMetrics(
    MetricBucket Aggregate,
    IReadOnlyDictionary<string, MetricBucket> PerLanguage,
    IReadOnlyDictionary<string, MetricBucket> PerTag);
