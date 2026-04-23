using FluentValidation;

namespace Starter.Module.Communication.Application.Commands.UpdateTemplateOverride;

public sealed class UpdateTemplateOverrideCommandValidator : AbstractValidator<UpdateTemplateOverrideCommand>
{
    public UpdateTemplateOverrideCommandValidator()
    {
        RuleFor(x => x.MessageTemplateId).NotEmpty();
        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("Body template is required.");
        RuleFor(x => x.SubjectTemplate)
            .MaximumLength(500).WithMessage("Subject template must not exceed 500 characters.")
            .When(x => x.SubjectTemplate is not null);
    }
}
