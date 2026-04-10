using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Capabilities.MetricCalculators;

/// <summary>
/// Calculates the current user count per tenant for <c>UsageTrackerService</c>
/// self-heal lookups. Core metric — always registered by <c>AddCapabilities</c>.
/// </summary>
internal sealed class UsersMetricCalculator(IApplicationDbContext context) : IUsageMetricCalculator
{
    public string Metric => "users";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId, cancellationToken);
}
