using FluentValidation;

namespace Starter.Module.AI.Application.Commands.InstallTemplate;

public sealed class InstallTemplateCommandValidator : AbstractValidator<InstallTemplateCommand>
{
    public InstallTemplateCommandValidator()
    {
        RuleFor(c => c.TemplateSlug)
            .NotEmpty()
            .MaximumLength(128);
    }
}
