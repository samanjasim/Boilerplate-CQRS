using FluentValidation;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantBranding;

public sealed class UpdateTenantBrandingCommandValidator : AbstractValidator<UpdateTenantBrandingCommand>
{
    public UpdateTenantBrandingCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Tenant ID is required.");

        RuleFor(x => x.PrimaryColor)
            .Matches(@"^#[0-9a-fA-F]{6}$").WithMessage("Primary color must be a valid hex color (e.g. #FF0000).")
            .When(x => x.PrimaryColor is not null);

        RuleFor(x => x.SecondaryColor)
            .Matches(@"^#[0-9a-fA-F]{6}$").WithMessage("Secondary color must be a valid hex color (e.g. #00FF00).")
            .When(x => x.SecondaryColor is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);
    }
}
