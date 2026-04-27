using FluentValidation;
using Starter.Application.Common.Interfaces;

namespace Starter.Module.AI.Application.Commands.SetAgentBudget;

public sealed class SetAgentBudgetCommandValidator : AbstractValidator<SetAgentBudgetCommand>
{
    public SetAgentBudgetCommandValidator(IFeatureFlagService ff)
    {
        RuleFor(x => x.MonthlyCostCapUsd)
            .GreaterThanOrEqualTo(0).When(x => x.MonthlyCostCapUsd.HasValue)
            .MustAsync(async (cap, ct) =>
                cap is null || cap <= await ff.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd", ct))
            .WithMessage("Per-agent monthly cap cannot exceed plan ceiling.");

        RuleFor(x => x.DailyCostCapUsd)
            .GreaterThanOrEqualTo(0).When(x => x.DailyCostCapUsd.HasValue)
            .MustAsync(async (cap, ct) =>
                cap is null || cap <= await ff.GetValueAsync<decimal>("ai.cost.tenant_daily_usd", ct))
            .WithMessage("Per-agent daily cap cannot exceed plan ceiling.");

        RuleFor(x => x.RequestsPerMinute)
            .GreaterThanOrEqualTo(0).When(x => x.RequestsPerMinute.HasValue)
            .MustAsync(async (rpm, ct) =>
                rpm is null || rpm <= await ff.GetValueAsync<int>("ai.agents.requests_per_minute_default", ct))
            .WithMessage("Per-agent rate limit cannot exceed plan default.");
    }
}
