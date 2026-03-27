using FluentValidation;

namespace Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;

public sealed class UpdateApiKeyCommandValidator : AbstractValidator<UpdateApiKeyCommand>
{
    public UpdateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("API key name must not exceed 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x)
            .Must(x => x.Name is not null || x.Scopes is not null)
            .WithMessage("At least one field (Name or Scopes) must be provided.");
    }
}
