using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.CreateChannelConfig;

public sealed class CreateChannelConfigCommandValidator : AbstractValidator<CreateChannelConfigCommand>
{
    public CreateChannelConfigCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters.");

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("Invalid notification channel.");

        RuleFor(x => x.Provider)
            .IsInEnum().WithMessage("Invalid channel provider.");

        RuleFor(x => x.Credentials)
            .NotEmpty().WithMessage("Credentials are required.");
    }
}
