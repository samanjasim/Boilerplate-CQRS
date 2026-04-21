using System.Diagnostics.Metrics;

namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Dedicated meter for retrieval circuit-breaker state transitions so the cardinality
/// footprint is independent from the main RAG pipeline meter.
/// </summary>
internal static class AiRagCircuitMetrics
{
    public const string MeterName = "Starter.Module.AI.Rag.Circuit";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> StateChanges =
        _meter.CreateCounter<long>(
            name: "rag.circuit.state_changes",
            unit: "count",
            description: "Circuit breaker state transitions tagged by service (qdrant|postgres-fts) and state (open|closed|half_open).");
}
