using FluentValidation;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

public sealed class UpdateAssistantCommandValidator : AbstractValidator<UpdateAssistantCommand>
{
    public UpdateAssistantCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        AssistantInputRules.Apply(this);
    }
}
