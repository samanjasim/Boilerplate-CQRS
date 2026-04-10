using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Capabilities.MetricCalculators;

/// <summary>
/// Calculates the total storage bytes consumed per tenant by summing
/// <c>FileMetadata.Size</c>. Core metric — always registered.
/// </summary>
internal sealed class StorageBytesMetricCalculator(IApplicationDbContext context) : IUsageMetricCalculator
{
    public string Metric => "storage_bytes";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.Set<FileMetadata>()
            .IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId)
            .SumAsync(f => f.Size, cancellationToken);
}
