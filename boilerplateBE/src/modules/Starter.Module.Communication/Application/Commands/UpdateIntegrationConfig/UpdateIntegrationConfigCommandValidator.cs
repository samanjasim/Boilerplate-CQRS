using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.UpdateIntegrationConfig;

public sealed class UpdateIntegrationConfigCommandValidator : AbstractValidator<UpdateIntegrationConfigCommand>
{
    public UpdateIntegrationConfigCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Integration config ID is required.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters.");
    }
}
