using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Webhooks.Infrastructure.Persistence;

namespace Starter.Module.Webhooks.Infrastructure.Services;

/// <summary>
/// Calculates the active webhook endpoint count per tenant for the
/// <c>"webhooks"</c> usage metric. Registered by <c>WebhooksModule</c> —
/// when Webhooks is not installed, the metric is simply not registered
/// and <c>UsageTrackerService</c> returns 0 silently.
///
/// Reads from the module's own <see cref="WebhooksDbContext"/>; core has
/// zero knowledge of the <c>WebhookEndpoint</c> entity.
/// </summary>
internal sealed class WebhookUsageMetricCalculator(WebhooksDbContext db) : IUsageMetricCalculator
{
    public string Metric => "webhooks";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await db.WebhookEndpoints
            .IgnoreQueryFilters()
            .CountAsync(w => w.TenantId == tenantId, cancellationToken);
}
