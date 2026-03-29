using FluentValidation;

namespace Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;

public sealed class SetTenantOverrideCommandValidator : AbstractValidator<SetTenantOverrideCommand>
{
    public SetTenantOverrideCommandValidator()
    {
        RuleFor(x => x.FeatureFlagId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(4000);
    }
}
