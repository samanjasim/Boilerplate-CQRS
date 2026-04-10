namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Capability contract for computing a tenant's current value of a named usage
/// metric from persistent storage. Used by <c>UsageTrackerService</c> as the
/// "self-heal" path when a Redis counter is missing or evicted.
///
/// Core registers implementations for the core metrics ("users", "api_keys",
/// "storage_bytes", "reports_active"). Modules that own their own counted
/// entities register their own calculator for their metric (e.g. the
/// Webhooks module registers a calculator for "webhooks").
///
/// If no calculator is registered for a metric, <c>UsageTrackerService</c>
/// returns whatever value Redis has (default 0) without attempting a fallback.
/// This means a module-absent build silently returns 0 for that module's
/// metrics — no error, no exception.
/// </summary>
public interface IUsageMetricCalculator : ICapability
{
    /// <summary>
    /// The metric name this calculator handles. Must match the metric string
    /// passed to <c>IUsageTracker.GetAsync</c> (e.g. "webhooks", "users").
    /// </summary>
    string Metric { get; }

    /// <summary>
    /// Compute the current value of this metric for the given tenant directly
    /// from persistent storage. Implementations should bypass tenant query
    /// filters (most use <c>IgnoreQueryFilters()</c>) and filter explicitly
    /// by <paramref name="tenantId"/>.
    /// </summary>
    Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
