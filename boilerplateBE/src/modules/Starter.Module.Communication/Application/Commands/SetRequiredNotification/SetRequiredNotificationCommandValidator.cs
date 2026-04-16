using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.SetRequiredNotification;

public sealed class SetRequiredNotificationCommandValidator : AbstractValidator<SetRequiredNotificationCommand>
{
    public SetRequiredNotificationCommandValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.");

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("Invalid notification channel.");
    }
}
