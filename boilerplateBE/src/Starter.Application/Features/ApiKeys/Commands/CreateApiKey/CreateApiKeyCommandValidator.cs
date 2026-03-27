using FluentValidation;

namespace Starter.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("API key name is required.")
            .MaximumLength(200).WithMessage("API key name must not exceed 200 characters.");

        RuleFor(x => x.Scopes)
            .NotEmpty().WithMessage("At least one scope is required.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("Expiration date must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }
}
