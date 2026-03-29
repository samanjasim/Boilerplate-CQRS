using FluentValidation;

namespace Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;

public sealed class CreateFeatureFlagCommandValidator : AbstractValidator<CreateFeatureFlagCommand>
{
    public CreateFeatureFlagCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(200)
            .Matches(@"^[a-z0-9_.]+$").WithMessage("Key must be lowercase alphanumeric with dots and underscores only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DefaultValue).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Category).IsInEnum();
    }
}
