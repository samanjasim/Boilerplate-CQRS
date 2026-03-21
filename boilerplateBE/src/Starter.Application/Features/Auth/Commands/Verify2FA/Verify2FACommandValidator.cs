using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.Verify2FA;

public sealed class Verify2FACommandValidator : AbstractValidator<Verify2FACommand>
{
    public Verify2FACommandValidator()
    {
        RuleFor(x => x.Secret)
            .NotEmpty().WithMessage("Secret is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required.")
            .Length(6).WithMessage("Verification code must be 6 digits.");
    }
}
