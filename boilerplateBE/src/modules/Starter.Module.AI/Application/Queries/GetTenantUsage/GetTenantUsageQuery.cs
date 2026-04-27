using MediatR;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTenantUsage;

public sealed record GetTenantUsageQuery() : IRequest<Result<TenantUsageDto>>;

public sealed record TenantUsageDto(
    Guid TenantId,
    long TotalInputTokensMonthly,
    long TotalOutputTokensMonthly,
    decimal TotalEstimatedCostUsdMonthly,
    decimal TotalEstimatedCostUsdDaily,
    int RunCountMonthly,
    int AgentCount,
    EffectiveCaps PlanCeilings);
