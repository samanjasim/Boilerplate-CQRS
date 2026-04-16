using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.UpdateChannelConfig;

public sealed class UpdateChannelConfigCommandValidator : AbstractValidator<UpdateChannelConfigCommand>
{
    public UpdateChannelConfigCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters.");
        RuleFor(x => x.Credentials)
            .NotEmpty().WithMessage("Credentials are required.");
    }
}
