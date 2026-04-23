using System.Diagnostics.Metrics;
using FluentAssertions;
using Starter.Api.Tests.Ai.Observability;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Eval.Latency;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

// Shares the process-global AiRagMetrics meter with tests in the Observability collection;
// must be serialized against them to avoid cross-test measurement pollution.
[Collection(ObservabilityTestCollection.Name)]
public sealed class StageLatencyAggregatorTests
{
    [Fact]
    public void Capture_RecordsStageDurationsFromRagMeter()
    {
        using var capture = StageLatencyAggregator.BeginCapture();

        AiRagMetrics.StageDuration.Record(12.0, new KeyValuePair<string, object?>("rag.stage", "embed-query"));
        AiRagMetrics.StageDuration.Record(25.0, new KeyValuePair<string, object?>("rag.stage", "embed-query"));
        AiRagMetrics.StageDuration.Record(4.0,  new KeyValuePair<string, object?>("rag.stage", "acl-resolve"));

        var durations = capture.Stop();

        durations["embed-query"].Should().BeEquivalentTo(new[] { 12.0, 25.0 });
        durations["acl-resolve"].Should().BeEquivalentTo(new[] { 4.0 });
    }

    [Fact]
    public void Aggregate_ComputesPercentiles()
    {
        var perStage = new Dictionary<string, List<double>>
        {
            ["vector-search[0]"] = new() { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }
        };

        var metrics = StageLatencyAggregator.Aggregate(perStage);

        metrics.PerStage["vector-search[0]"].P50.Should().BeApproximately(50, 0.1);
        metrics.PerStage["vector-search[0]"].P95.Should().BeApproximately(100, 0.1);
        metrics.PerStage["vector-search[0]"].P99.Should().BeApproximately(100, 0.1);
    }

    [Fact]
    public void Aggregate_EmptyPerStage_ReturnsEmptyMetrics()
    {
        var metrics = StageLatencyAggregator.Aggregate(new Dictionary<string, List<double>>());
        metrics.PerStage.Should().BeEmpty();
    }
}
