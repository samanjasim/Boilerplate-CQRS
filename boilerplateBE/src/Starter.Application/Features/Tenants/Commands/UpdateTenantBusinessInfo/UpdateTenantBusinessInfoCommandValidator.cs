using FluentValidation;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantBusinessInfo;

public sealed class UpdateTenantBusinessInfoCommandValidator : AbstractValidator<UpdateTenantBusinessInfoCommand>
{
    public UpdateTenantBusinessInfoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Tenant ID is required.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.")
            .When(x => x.Address is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Website)
            .MaximumLength(200).WithMessage("Website must not exceed 200 characters.")
            .Must(BeAValidUrl).WithMessage("Website must be a valid URL.")
            .When(x => x.Website is not null);

        RuleFor(x => x.TaxId)
            .MaximumLength(100).WithMessage("Tax ID must not exceed 100 characters.")
            .When(x => x.TaxId is not null);
    }

    private static bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var result)
            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
