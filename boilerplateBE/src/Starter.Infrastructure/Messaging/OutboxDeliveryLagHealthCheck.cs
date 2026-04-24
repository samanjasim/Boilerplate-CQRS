using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Messaging;

/// <summary>
/// Monitors the depth and age of the MassTransit outbox table. Reports:
/// <list type="bullet">
///   <item><c>Healthy</c> — all pending rows are young and few</item>
///   <item><c>Degraded</c> — delivery is lagging; investigate the BusOutboxDeliveryService</item>
/// </list>
///
/// Intentionally never reports <c>Unhealthy</c>: a backlogged outbox is a
/// delivery problem, not an API-readiness problem. Liveness probes should
/// not restart the pod over it. Ops tooling should alert on the
/// <c>outbox-delivery-lag</c> Degraded signal.
///
/// <para>
/// <b>Why raw SQL?</b> MT's <c>OutboxMessage</c> schema (column names, nullability)
/// changes across minor versions and isn't part of MT's public contract. A raw
/// SQL aggregate query is stable: if MT renames a column we see a runtime error
/// in the health check — loud, localized, easy to fix — instead of a silent LINQ
/// translation drift.
/// </para>
/// </summary>
public sealed class OutboxDeliveryLagHealthCheck : IHealthCheck
{
    // MT 8.x: BusOutboxDeliveryService physically removes OutboxMessage rows
    // after successful delivery (see MassTransit.EntityFrameworkCoreIntegration.
    // BusOutboxDeliveryService.RemoveOutbox / RemoveOutboxMessages). So every
    // row in OutboxMessage is pending by definition, and MIN(EnqueueTime) is
    // the age of the backlog's oldest message. No status column required.
    private const string LagSql = """
        SELECT
            COUNT(*)::int AS pending_count,
            EXTRACT(EPOCH FROM (NOW() - MIN("EnqueueTime")))::double precision AS oldest_age_seconds
        FROM "OutboxMessage"
        """;

    private readonly ApplicationDbContext _db;
    private readonly OutboxHealthCheckOptions _options;

    public OutboxDeliveryLagHealthCheck(
        ApplicationDbContext db,
        IOptions<OutboxHealthCheckOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = LagSql;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy("outbox query returned no row");
            }

            var pendingCount = reader.GetInt32(0);
            // MIN() over empty set returns NULL → oldest_age_seconds is DBNull.
            var oldestAgeSeconds = reader.IsDBNull(1) ? 0d : reader.GetDouble(1);

            if (pendingCount == 0)
            {
                return HealthCheckResult.Healthy(
                    "outbox is empty",
                    data: new Dictionary<string, object> { ["pending_count"] = 0 });
            }

            var data = new Dictionary<string, object>
            {
                ["pending_count"] = pendingCount,
                ["oldest_age_seconds"] = Math.Round(oldestAgeSeconds, 1),
                ["threshold_max_rows"] = _options.MaxPendingRows,
                ["threshold_max_age_seconds"] = _options.MaxOldestAge.TotalSeconds,
            };

            var lagExceedsThreshold =
                pendingCount > _options.MaxPendingRows ||
                oldestAgeSeconds > _options.MaxOldestAge.TotalSeconds;

            if (lagExceedsThreshold)
            {
                return HealthCheckResult.Degraded(
                    description:
                        $"outbox delivery lagging: {pendingCount} pending row(s), " +
                        $"oldest is {oldestAgeSeconds:F0}s old",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                description: $"outbox healthy: {pendingCount} pending, oldest {oldestAgeSeconds:F0}s",
                data: data);
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
