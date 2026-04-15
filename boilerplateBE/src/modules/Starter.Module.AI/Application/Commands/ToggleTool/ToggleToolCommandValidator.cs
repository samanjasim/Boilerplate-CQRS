using FluentValidation;

namespace Starter.Module.AI.Application.Commands.ToggleTool;

public sealed class ToggleToolCommandValidator : AbstractValidator<ToggleToolCommand>
{
    public ToggleToolCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}
