using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiUsageMetricCalculator(AiDbContext db) : IUsageMetricCalculator
{
    public string Metric => "ai_tokens";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return await db.AiUsageLogs
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId && l.CreatedAt >= periodStart)
            .SumAsync(l => (long)l.InputTokens + l.OutputTokens, cancellationToken);
    }
}
