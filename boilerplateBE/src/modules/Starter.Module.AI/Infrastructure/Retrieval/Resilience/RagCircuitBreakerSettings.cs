namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

public sealed class RagCircuitBreakerSettings
{
    public RagCircuitBreakerOptions Qdrant { get; init; } = new();
    public RagCircuitBreakerOptions PostgresFts { get; init; } = new();
}
