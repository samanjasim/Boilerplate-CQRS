using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.UpdatePublicWidget;

public sealed class UpdatePublicWidgetCommandValidator : AbstractValidator<UpdatePublicWidgetCommand>
{
    public UpdatePublicWidgetCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AllowedOrigins).NotEmpty();
        RuleForEach(x => x.AllowedOrigins).NotEmpty().MaximumLength(512);
        RuleFor(x => x.DefaultPersonaSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.MonthlyTokenCap).GreaterThanOrEqualTo(0).When(x => x.MonthlyTokenCap.HasValue);
        RuleFor(x => x.DailyTokenCap).GreaterThanOrEqualTo(0).When(x => x.DailyTokenCap.HasValue);
        RuleFor(x => x.RequestsPerMinute).GreaterThanOrEqualTo(0).When(x => x.RequestsPerMinute.HasValue);
    }
}
