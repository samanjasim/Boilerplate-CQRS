using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTenantUsage;

internal sealed class GetTenantUsageQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IFeatureFlagService featureFlags) : IRequestHandler<GetTenantUsageQuery, Result<TenantUsageDto>>
{
    public async Task<Result<TenantUsageDto>> Handle(GetTenantUsageQuery request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<TenantUsageDto>(Error.Unauthorized());

        // Postgres `timestamp with time zone` rejects DateTime with Kind=Unspecified.
        // Build window boundaries explicitly as UTC.
        var nowUtc = DateTime.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayStart = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);

        var monthly = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.CreatedAt >= monthStart)
            .GroupBy(l => 1)
            .Select(g => new
            {
                Input = g.Sum(x => (long)x.InputTokens),
                Output = g.Sum(x => (long)x.OutputTokens),
                Cost = g.Sum(x => x.EstimatedCost),
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        var dailyCost = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.CreatedAt >= dayStart)
            .SumAsync(l => l.EstimatedCost, ct);

        var agentCount = await db.AiAssistants
            .AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId, ct);

        var planMonthly = await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", ct);
        var planDaily = await featureFlags.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", ct);
        var planRpm = await featureFlags.GetValueAsync<int>("ai.agents.requests_per_minute_default", ct);

        return Result.Success(new TenantUsageDto(
            TenantId: tenantId.Value,
            TotalInputTokensMonthly: monthly?.Input ?? 0,
            TotalOutputTokensMonthly: monthly?.Output ?? 0,
            TotalEstimatedCostUsdMonthly: monthly?.Cost ?? 0m,
            TotalEstimatedCostUsdDaily: dailyCost,
            RunCountMonthly: monthly?.Count ?? 0,
            AgentCount: agentCount,
            PlanCeilings: new EffectiveCaps(planMonthly, planDaily, planRpm)));
    }
}
