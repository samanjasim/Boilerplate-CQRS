using FluentValidation;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantCustomText;

public sealed class UpdateTenantCustomTextCommandValidator : AbstractValidator<UpdateTenantCustomTextCommand>
{
    public UpdateTenantCustomTextCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Tenant ID is required.");

        RuleFor(x => x.LoginPageTitle)
            .MaximumLength(2000).WithMessage("Login page title must not exceed 2000 characters.")
            .When(x => x.LoginPageTitle is not null);

        RuleFor(x => x.LoginPageSubtitle)
            .MaximumLength(2000).WithMessage("Login page subtitle must not exceed 2000 characters.")
            .When(x => x.LoginPageSubtitle is not null);

        RuleFor(x => x.EmailFooterText)
            .MaximumLength(2000).WithMessage("Email footer text must not exceed 2000 characters.")
            .When(x => x.EmailFooterText is not null);
    }
}
