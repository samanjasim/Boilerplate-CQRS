using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;

namespace Starter.Infrastructure.Capabilities.MetricCalculators;

/// <summary>
/// Calculates the active (non-revoked) API key count per tenant.
/// Core metric — always registered by <c>AddCapabilities</c>.
/// </summary>
internal sealed class ApiKeysMetricCalculator(IApplicationDbContext context) : IUsageMetricCalculator
{
    public string Metric => "api_keys";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.Set<ApiKey>()
            .IgnoreQueryFilters()
            .CountAsync(k => k.TenantId == tenantId && !k.IsRevoked, cancellationToken);
}
