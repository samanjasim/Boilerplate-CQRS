using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.UpdateNotificationPreferences;

public sealed class UpdateNotificationPreferencesCommandValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(x => x.Preferences)
            .NotEmpty().WithMessage("At least one preference is required.");

        RuleForEach(x => x.Preferences).ChildRules(item =>
        {
            item.RuleFor(p => p.Category)
                .NotEmpty().WithMessage("Category is required.");
        });
    }
}
