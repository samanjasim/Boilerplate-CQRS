using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

/// <summary>
/// Calculates the delivered message count per tenant for the current month
/// for the <c>"messages"</c> usage metric. Registered by <c>CommunicationModule</c>.
/// </summary>
internal sealed class CommunicationUsageMetricCalculator(CommunicationDbContext db) : IUsageMetricCalculator
{
    public string Metric => "messages";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return await db.DeliveryLogs
            .IgnoreQueryFilters()
            .CountAsync(d =>
                d.TenantId == tenantId &&
                d.Status == DeliveryStatus.Delivered &&
                d.CreatedAt >= startOfMonth,
                cancellationToken);
    }
}
