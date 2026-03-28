using FluentValidation;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed class UpdateFeatureFlagCommandValidator : AbstractValidator<UpdateFeatureFlagCommand>
{
    public UpdateFeatureFlagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DefaultValue).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}
