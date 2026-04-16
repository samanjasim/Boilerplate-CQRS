using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.CreateIntegrationConfig;

public sealed class CreateIntegrationConfigCommandValidator : AbstractValidator<CreateIntegrationConfigCommand>
{
    public CreateIntegrationConfigCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters.");

        RuleFor(x => x.IntegrationType)
            .IsInEnum().WithMessage("Invalid integration type.");

        RuleFor(x => x.Credentials)
            .NotEmpty().WithMessage("Credentials are required.");
    }
}
