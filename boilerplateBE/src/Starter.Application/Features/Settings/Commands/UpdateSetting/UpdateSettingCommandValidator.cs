using FluentValidation;

namespace Starter.Application.Features.Settings.Commands.UpdateSetting;

public sealed class UpdateSettingCommandValidator : AbstractValidator<UpdateSettingCommand>
{
    public UpdateSettingCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Setting key is required.")
            .MaximumLength(200).WithMessage("Setting key must not exceed 200 characters.");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Setting value is required.")
            .MaximumLength(4000).WithMessage("Setting value must not exceed 4000 characters.");
    }
}
