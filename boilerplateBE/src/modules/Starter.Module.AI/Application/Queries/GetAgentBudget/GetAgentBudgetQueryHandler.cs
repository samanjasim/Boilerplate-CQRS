using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAgentBudget;

internal sealed class GetAgentBudgetQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    ICostCapResolver capResolver,
    ICostCapAccountant accountant) : IRequestHandler<GetAgentBudgetQuery, Result<AgentBudgetDto>>
{
    public async Task<Result<AgentBudgetDto>> Handle(GetAgentBudgetQuery request, CancellationToken ct)
    {
        var assistant = await db.AiAssistants
            .AsNoTracking()
            .Where(a => a.Id == request.AssistantId)
            .Select(a => new
            {
                a.Id,
                a.TenantId,
                a.MonthlyCostCapUsd,
                a.DailyCostCapUsd,
                a.RequestsPerMinute
            })
            .FirstOrDefaultAsync(ct);

        if (assistant is null)
            return Result.Failure<AgentBudgetDto>(AiErrors.AssistantNotFound);

        var tenantId = assistant.TenantId ?? currentUser.TenantId ?? Guid.Empty;
        var caps = await capResolver.ResolveAsync(tenantId, assistant.Id, ct);
        var monthly = await accountant.GetCurrentAsync(tenantId, assistant.Id, CapWindow.Monthly, ct);
        var daily = await accountant.GetCurrentAsync(tenantId, assistant.Id, CapWindow.Daily, ct);

        return Result.Success(new AgentBudgetDto(
            AssistantId: assistant.Id,
            PerAgentMonthlyCostCapUsd: assistant.MonthlyCostCapUsd,
            PerAgentDailyCostCapUsd: assistant.DailyCostCapUsd,
            PerAgentRequestsPerMinute: assistant.RequestsPerMinute,
            EffectiveCaps: caps,
            CurrentMonthlyUsd: monthly,
            CurrentDailyUsd: daily));
    }
}
