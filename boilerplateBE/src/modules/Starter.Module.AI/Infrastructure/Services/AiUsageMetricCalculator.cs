using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiUsageMetricCalculator(AiDbContext db) : IUsageMetricCalculator
{
    public string Metric => "ai_tokens";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await db.AiUsageLogs
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId)
            .SumAsync(l => (long)l.InputTokens + l.OutputTokens, cancellationToken);
}
