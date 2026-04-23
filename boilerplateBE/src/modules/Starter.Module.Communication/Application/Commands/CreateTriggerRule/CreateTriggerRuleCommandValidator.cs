using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.CreateTriggerRule;

public sealed class CreateTriggerRuleCommandValidator : AbstractValidator<CreateTriggerRuleCommand>
{
    public CreateTriggerRuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.EventName)
            .NotEmpty().WithMessage("Event name is required.")
            .MaximumLength(200).WithMessage("Event name must not exceed 200 characters.");

        RuleFor(x => x.MessageTemplateId)
            .NotEmpty().WithMessage("Message template is required.");

        RuleFor(x => x.RecipientMode)
            .NotEmpty().WithMessage("Recipient mode is required.");

        RuleFor(x => x.ChannelSequence)
            .NotEmpty().WithMessage("At least one channel is required.");
    }
}
