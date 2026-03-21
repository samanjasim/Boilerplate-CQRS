using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.VerifyEmail;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required.")
            .Length(6).WithMessage("Verification code must be 6 digits.")
            .Matches("^[0-9]{6}$").WithMessage("Verification code must contain only digits.");
    }
}
