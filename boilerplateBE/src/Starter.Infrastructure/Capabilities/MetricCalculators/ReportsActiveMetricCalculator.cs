using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Capabilities.MetricCalculators;

/// <summary>
/// Calculates the active report request count per tenant.
/// Core metric — always registered.
/// </summary>
internal sealed class ReportsActiveMetricCalculator(IApplicationDbContext context) : IUsageMetricCalculator
{
    public string Metric => "reports_active";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.Set<ReportRequest>()
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId, cancellationToken);
}
