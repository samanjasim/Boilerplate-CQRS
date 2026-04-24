using System.Text.RegularExpressions;
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Personas.UpdatePersona;

public sealed class UpdatePersonaCommandValidator : AbstractValidator<UpdatePersonaCommand>
{
    private static readonly Regex SlugRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public UpdatePersonaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SafetyPreset).IsInEnum();
        RuleForEach(x => x.PermittedAgentSlugs!)
            .Must(s => SlugRegex.IsMatch(s))
            .When(x => x.PermittedAgentSlugs is not null);
    }
}
