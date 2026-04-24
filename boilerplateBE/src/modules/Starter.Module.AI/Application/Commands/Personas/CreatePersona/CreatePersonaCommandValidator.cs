using System.Text.RegularExpressions;
using FluentValidation;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Commands.Personas.CreatePersona;

public sealed class CreatePersonaCommandValidator : AbstractValidator<CreatePersonaCommand>
{
    private static readonly Regex SlugRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public CreatePersonaCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.AudienceType)
            .IsInEnum()
            .NotEqual(PersonaAudienceType.Anonymous)
            .WithMessage("Anonymous audience is reserved for the system-managed anonymous persona.");
        RuleFor(x => x.SafetyPreset).IsInEnum();
        RuleFor(x => x.Slug!)
            .Must(s => SlugRegex.IsMatch(s))
            .When(x => !string.IsNullOrEmpty(x.Slug))
            .WithMessage("Slug must be lowercase kebab-case.");
        RuleFor(x => x.Slug)
            .Must(s => s is null || (s != AiPersona.AnonymousSlug && s != AiPersona.DefaultSlug))
            .WithMessage("Slug 'anonymous' and 'default' are reserved.");
        RuleForEach(x => x.PermittedAgentSlugs!)
            .Must(s => SlugRegex.IsMatch(s))
            .When(x => x.PermittedAgentSlugs is not null)
            .WithMessage("Each permitted agent slug must be kebab-case.");
    }
}
