using FluentAssertions;
using Polly.CircuitBreaker;
using Starter.Api.Tests.Ai.Observability;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

[Collection(ObservabilityTestCollection.Name)]
public class WithTimeoutAsyncCircuitOpenOutcomeTests
{
    [Fact]
    public async Task Open_circuit_produces_circuit_open_outcome_and_degrades_stage()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: _ => throw new BrokenCircuitException("qdrant breaker open"),
            timeoutMs: 500,
            stageName: "vector-search",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("vector-search");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome"
                        && (string?)m.Tags["rag.stage"] == "vector-search")
            .ToList();

        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "circuit_open");
    }
}
