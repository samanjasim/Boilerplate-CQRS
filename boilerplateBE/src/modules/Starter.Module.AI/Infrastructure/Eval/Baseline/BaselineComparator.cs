namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public sealed record BaselineComparisonResult(
    bool Failed,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Warnings);

public static class BaselineComparator
{
    public static BaselineComparisonResult Compare(
        BaselineDatasetSnapshot baseline,
        BaselineDatasetSnapshot current,
        double metricTolerance,
        double latencyTolerance)
    {
        var failures = new List<string>();
        var warnings = new List<string>();

        CompareMetricDict("recall_at_", baseline.RecallAtK, current.RecallAtK,
            metricTolerance, failures, warnings);
        CompareMetricDict("precision_at_", baseline.PrecisionAtK, current.PrecisionAtK,
            metricTolerance, failures, warnings);
        CompareMetricDict("ndcg_at_", baseline.NdcgAtK, current.NdcgAtK,
            metricTolerance, failures, warnings);
        CompareMetricDict("hit_rate_at_", baseline.HitRateAtK, current.HitRateAtK,
            metricTolerance, failures, warnings);
        CompareSingleMetric("mrr", baseline.Mrr, current.Mrr,
            metricTolerance, failures, warnings);

        foreach (var (stage, baseP95) in baseline.StageP95Ms)
        {
            if (!current.StageP95Ms.TryGetValue(stage, out var curP95)) continue;
            if (baseP95 <= 0) continue;
            var delta = (curP95 - baseP95) / baseP95;
            if (delta > latencyTolerance)
                failures.Add($"latency.{stage}.p95 regressed: {baseP95:F1} → {curP95:F1} ms ({delta:P1})");
        }

        if (current.DegradedStageCount > baseline.DegradedStageCount)
            failures.Add(
                $"degraded_stage_count increased: {baseline.DegradedStageCount} → {current.DegradedStageCount}");

        return new BaselineComparisonResult(failures.Count > 0, failures, warnings);
    }

    private static void CompareMetricDict(
        string prefix,
        IReadOnlyDictionary<int, double> baseline,
        IReadOnlyDictionary<int, double> current,
        double tolerance,
        List<string> failures,
        List<string> warnings)
    {
        foreach (var (k, baseValue) in baseline)
        {
            if (!current.TryGetValue(k, out var curValue)) continue;
            CompareSingleMetric($"{prefix}{k}", baseValue, curValue, tolerance, failures, warnings);
        }
    }

    private static void CompareSingleMetric(
        string name,
        double baseline,
        double current,
        double tolerance,
        List<string> failures,
        List<string> warnings)
    {
        if (baseline <= 0) return;
        var delta = (current - baseline) / baseline;
        if (delta < -tolerance)
            failures.Add($"{name} regressed: {baseline:F4} → {current:F4} ({delta:P1})");
        else if (delta > tolerance)
            warnings.Add($"{name} improved past tolerance: {baseline:F4} → {current:F4} ({delta:P1})");
    }
}
