using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SetAgentBudget;

public sealed record SetAgentBudgetCommand(
    Guid AssistantId,
    decimal? MonthlyCostCapUsd,
    decimal? DailyCostCapUsd,
    int? RequestsPerMinute) : IRequest<Result>;
