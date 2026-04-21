using FluentAssertions;
using Starter.Api.Tests.Ai.Observability;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class AiRagCircuitMetricsTests
{
    [Fact]
    public void StateChanges_emits_measurement_with_service_and_state_tags()
    {
        using var listener = new TestMeterListener(AiRagCircuitMetrics.MeterName);

        AiRagCircuitMetrics.StateChanges.Add(
            1,
            new KeyValuePair<string, object?>("rag.circuit.service", "qdrant"),
            new KeyValuePair<string, object?>("rag.circuit.state", "open"));

        var snap = listener.Snapshot();
        snap.Should().ContainSingle(m =>
            m.InstrumentName == "rag.circuit.state_changes" &&
            (string?)m.Tags["rag.circuit.service"] == "qdrant" &&
            (string?)m.Tags["rag.circuit.state"] == "open" &&
            m.Value == 1);
    }

    [Fact]
    public void Meter_name_is_distinct_from_rag_meter()
    {
        AiRagCircuitMetrics.MeterName.Should().NotBe(AiRagMetrics.MeterName);
    }
}
