namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record StagePercentiles(double P50, double P95, double P99);

public sealed record LatencyMetrics(IReadOnlyDictionary<string, StagePercentiles> PerStage);
