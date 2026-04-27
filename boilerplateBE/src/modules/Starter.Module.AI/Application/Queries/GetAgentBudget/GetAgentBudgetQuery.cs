using MediatR;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAgentBudget;

public sealed record GetAgentBudgetQuery(Guid AssistantId) : IRequest<Result<AgentBudgetDto>>;

public sealed record AgentBudgetDto(
    Guid AssistantId,
    decimal? PerAgentMonthlyCostCapUsd,
    decimal? PerAgentDailyCostCapUsd,
    int? PerAgentRequestsPerMinute,
    EffectiveCaps EffectiveCaps,
    decimal CurrentMonthlyUsd,
    decimal CurrentDailyUsd);
