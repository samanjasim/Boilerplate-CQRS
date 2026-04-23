using FluentValidation;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

public sealed class CreateAssistantCommandValidator : AbstractValidator<CreateAssistantCommand>
{
    public CreateAssistantCommandValidator()
    {
        AssistantInputRules.Apply(this);
    }
}
