using FluentValidation;

namespace Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;

public sealed class SetTenantDefaultRoleCommandValidator : AbstractValidator<SetTenantDefaultRoleCommand>
{
    public SetTenantDefaultRoleCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required.");
    }
}
