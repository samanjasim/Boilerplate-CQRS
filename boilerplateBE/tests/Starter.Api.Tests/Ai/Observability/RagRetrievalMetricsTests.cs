using System.Diagnostics.Metrics;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class RagRetrievalMetricsTests
{
    [Fact]
    public async Task WithTimeoutAsync_records_duration_and_success_on_happy_path()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync(
            op: async ct => { await Task.Delay(5, ct); return "ok"; },
            timeoutMs: 500,
            stageName: "vector-search",
            degraded: degraded);

        result.Should().Be("ok");
        degraded.Should().BeEmpty();

        var snapshot = listener.Snapshot();
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.duration"
                                       && (string?)m.Tags["rag.stage"] == "vector-search");
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.outcome"
                                       && (string?)m.Tags["rag.stage"] == "vector-search"
                                       && (string?)m.Tags["rag.outcome"] == "success"
                                       && m.Value == 1);
    }

    [Fact]
    public async Task WithTimeoutAsync_records_timeout_outcome_when_op_exceeds_budget()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: async ct => { await Task.Delay(200, ct); return "too-slow"; },
            timeoutMs: 20,
            stageName: "rerank",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("rerank");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "timeout");
    }

    [Fact]
    public async Task WithTimeoutAsync_records_error_outcome_on_exception()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: _ => throw new InvalidOperationException("boom"),
            timeoutMs: 500,
            stageName: "rewrite",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("rewrite");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "error");
    }
}
