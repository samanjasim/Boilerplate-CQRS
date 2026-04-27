using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAgentUsage;

internal sealed class GetAgentUsageQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetAgentUsageQuery, Result<AgentUsageDto>>
{
    public async Task<Result<AgentUsageDto>> Handle(GetAgentUsageQuery request, CancellationToken ct)
    {
        var window = (request.Window ?? "monthly").ToLowerInvariant();
        var since = window switch
        {
            "daily" => DateTime.UtcNow.Date,
            _ => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
        };

        var tenantId = currentUser.TenantId;
        var rows = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.AiAssistantId == request.AssistantId
                        && (tenantId == null || l.TenantId == tenantId)
                        && l.CreatedAt >= since)
            .GroupBy(l => 1)
            .Select(g => new
            {
                Input = g.Sum(x => (long)x.InputTokens),
                Output = g.Sum(x => (long)x.OutputTokens),
                Cost = g.Sum(x => x.EstimatedCost),
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        return Result.Success(new AgentUsageDto(
            AssistantId: request.AssistantId,
            Window: window,
            TotalInputTokens: rows?.Input ?? 0,
            TotalOutputTokens: rows?.Output ?? 0,
            TotalEstimatedCostUsd: rows?.Cost ?? 0m,
            RunCount: rows?.Count ?? 0));
    }
}
