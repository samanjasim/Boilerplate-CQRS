using FluentValidation;

namespace Starter.Application.Features.Settings.Commands.UpdateSettings;

public sealed class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        RuleFor(x => x.Settings)
            .NotEmpty().WithMessage("At least one setting is required.");

        RuleForEach(x => x.Settings).ChildRules(setting =>
        {
            setting.RuleFor(s => s.Key)
                .NotEmpty().WithMessage("Setting key is required.")
                .MaximumLength(200).WithMessage("Setting key must not exceed 200 characters.");

            setting.RuleFor(s => s.Value)
                .NotEmpty().WithMessage("Setting value is required.")
                .MaximumLength(4000).WithMessage("Setting value must not exceed 4000 characters.");
        });
    }
}
