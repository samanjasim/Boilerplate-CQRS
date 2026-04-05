using FluentValidation;

namespace Starter.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

public sealed class GetDashboardAnalyticsQueryValidator : AbstractValidator<GetDashboardAnalyticsQuery>
{
    private static readonly string[] ValidPeriods = ["7d", "30d", "90d", "12m"];

    public GetDashboardAnalyticsQueryValidator()
    {
        RuleFor(x => x.Period)
            .NotEmpty().WithMessage("Period is required.")
            .Must(p => ValidPeriods.Contains(p))
            .WithMessage("Invalid period. Valid values: 7d, 30d, 90d, 12m.");
    }
}
