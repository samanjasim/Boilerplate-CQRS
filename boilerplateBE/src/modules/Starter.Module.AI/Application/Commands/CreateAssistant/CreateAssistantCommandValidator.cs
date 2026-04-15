using FluentValidation;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

public sealed class CreateAssistantCommandValidator : AbstractValidator<CreateAssistantCommand>
{
    public CreateAssistantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SystemPrompt).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Model).MaximumLength(120);
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 64_000);
        RuleFor(x => x.MaxAgentSteps).InclusiveBetween(1, 50);
        RuleForEach(x => x.EnabledToolNames!)
            .NotEmpty().MaximumLength(120)
            .When(x => x.EnabledToolNames is not null);
    }
}
