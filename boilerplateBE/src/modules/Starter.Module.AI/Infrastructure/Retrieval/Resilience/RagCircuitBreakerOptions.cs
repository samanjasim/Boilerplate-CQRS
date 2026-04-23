namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

public sealed class RagCircuitBreakerOptions
{
    /// <summary>
    /// When false, calls bypass the breaker entirely (no sampling, no tripping).
    /// Use in dev environments where the backend is expected to flap.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Minimum number of executions within the 30-second sampling window before the
    /// breaker can trip. Too low a value causes trips on incidental failures.
    /// </summary>
    public int MinimumThroughput { get; init; } = 10;

    /// <summary>
    /// Failure ratio in [0, 1] that triggers the trip once <see cref="MinimumThroughput"/>
    /// is reached.
    /// </summary>
    public double FailureRatio { get; init; } = 0.5;

    /// <summary>
    /// Time the breaker stays open before transitioning to half-open and allowing
    /// one probe request through.
    /// </summary>
    public int BreakDurationMs { get; init; } = 30_000;
}
