using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAgentUsage;

public sealed record GetAgentUsageQuery(Guid AssistantId, string Window) : IRequest<Result<AgentUsageDto>>;

public sealed record AgentUsageDto(
    Guid AssistantId,
    string Window,
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal TotalEstimatedCostUsd,
    int RunCount);
