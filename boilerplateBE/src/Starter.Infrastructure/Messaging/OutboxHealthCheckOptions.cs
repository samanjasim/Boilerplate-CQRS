namespace Starter.Infrastructure.Messaging;

/// <summary>
/// Thresholds for <see cref="OutboxDeliveryLagHealthCheck"/>. Exceeding either
/// of these marks the health check as <c>Degraded</c> — not <c>Unhealthy</c> —
/// so an infrastructure probe that triggers on Unhealthy doesn't restart the
/// pod just because the delivery service is behind. Ops dashboards should
/// watch the Degraded signal and alert.
/// </summary>
public sealed class OutboxHealthCheckOptions
{
    public const string SectionName = "Outbox:HealthCheck";

    /// <summary>
    /// If more than this many outbox rows remain undelivered, report Degraded.
    /// </summary>
    public int MaxPendingRows { get; set; } = 1_000;

    /// <summary>
    /// If the oldest undelivered outbox row is older than this, report Degraded.
    /// </summary>
    public TimeSpan MaxOldestAge { get; set; } = TimeSpan.FromMinutes(5);
}
