using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Baseline;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class BaselineComparatorTests
{
    private static BaselineDatasetSnapshot Snap(
        double recall5 = 0.8, double mrr = 0.7,
        IReadOnlyDictionary<string, double>? stagesP95 = null,
        int degraded = 0)
    {
        var stages = stagesP95 ?? new Dictionary<string, double> { ["total"] = 100 };
        return new BaselineDatasetSnapshot(
            RecallAtK: new Dictionary<int, double> { [5] = recall5 },
            PrecisionAtK: new Dictionary<int, double>(),
            NdcgAtK: new Dictionary<int, double>(),
            HitRateAtK: new Dictionary<int, double>(),
            Mrr: mrr,
            StageP95Ms: stages,
            DegradedStageCount: degraded);
    }

    [Fact]
    public void MetricDropWithinTolerance_Passes()
    {
        var baseline = Snap(recall5: 0.80);
        var current = Snap(recall5: 0.78);
        var result = BaselineComparator.Compare(
            baseline, current, metricTolerance: 0.05, latencyTolerance: 0.20);
        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void MetricDropExceedsTolerance_Fails()
    {
        var baseline = Snap(recall5: 0.80);
        var current = Snap(recall5: 0.60);
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainMatch("*recall_at_5*");
    }

    [Fact]
    public void MetricImprovementPastTolerance_PassesWithWarning()
    {
        var baseline = Snap(recall5: 0.80);
        var current = Snap(recall5: 0.95);
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeFalse();
        result.Warnings.Should().ContainMatch("*recall_at_5*");
    }

    [Fact]
    public void LatencyP95IncreaseExceedsTolerance_Fails()
    {
        var baseline = Snap(stagesP95: new Dictionary<string, double> { ["rerank"] = 100 });
        var current  = Snap(stagesP95: new Dictionary<string, double> { ["rerank"] = 150 });
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainMatch("*rerank*");
    }

    [Fact]
    public void LatencyBelowNoiseFloor_IgnoresJitter()
    {
        // Baseline < 5 ms — jitter at this scale should not trigger a failure
        // even with a 400% swing, because the signal is dominated by noise.
        var baseline = Snap(stagesP95: new Dictionary<string, double> { ["classify"] = 0.8 });
        var current  = Snap(stagesP95: new Dictionary<string, double> { ["classify"] = 4.0 });
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void DegradedStageCountIncrease_Fails()
    {
        var baseline = Snap(degraded: 0);
        var current = Snap(degraded: 3);
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainMatch("*degraded*");
    }
}
