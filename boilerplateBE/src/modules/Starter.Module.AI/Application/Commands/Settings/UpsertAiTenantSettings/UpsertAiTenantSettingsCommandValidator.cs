using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;

public sealed class UpsertAiTenantSettingsCommandValidator : AbstractValidator<UpsertAiTenantSettingsCommand>
{
    public UpsertAiTenantSettingsCommandValidator()
    {
        RuleFor(x => x.MonthlyCostCapUsd).GreaterThanOrEqualTo(0).When(x => x.MonthlyCostCapUsd.HasValue);
        RuleFor(x => x.DailyCostCapUsd).GreaterThanOrEqualTo(0).When(x => x.DailyCostCapUsd.HasValue);
        RuleFor(x => x.PlatformMonthlyCostCapUsd).GreaterThanOrEqualTo(0).When(x => x.PlatformMonthlyCostCapUsd.HasValue);
        RuleFor(x => x.PlatformDailyCostCapUsd).GreaterThanOrEqualTo(0).When(x => x.PlatformDailyCostCapUsd.HasValue);
        RuleFor(x => x.RequestsPerMinute).GreaterThanOrEqualTo(0).When(x => x.RequestsPerMinute.HasValue);
        RuleFor(x => x.PublicMonthlyTokenCap).GreaterThanOrEqualTo(0).When(x => x.PublicMonthlyTokenCap.HasValue);
        RuleFor(x => x.PublicDailyTokenCap).GreaterThanOrEqualTo(0).When(x => x.PublicDailyTokenCap.HasValue);
        RuleFor(x => x.PublicRequestsPerMinute).GreaterThanOrEqualTo(0).When(x => x.PublicRequestsPerMinute.HasValue);
    }
}
