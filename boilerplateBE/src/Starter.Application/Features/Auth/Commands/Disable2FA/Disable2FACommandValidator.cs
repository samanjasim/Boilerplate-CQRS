using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.Disable2FA;

public sealed class Disable2FACommandValidator : AbstractValidator<Disable2FACommand>
{
    public Disable2FACommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required.");
    }
}
