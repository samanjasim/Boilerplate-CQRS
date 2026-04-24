using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Personas.AssignPersona;

public sealed class AssignPersonaCommandValidator : AbstractValidator<AssignPersonaCommand>
{
    public AssignPersonaCommandValidator()
    {
        RuleFor(x => x.PersonaId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
