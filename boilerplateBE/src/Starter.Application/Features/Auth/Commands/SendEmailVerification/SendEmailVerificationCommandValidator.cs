using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.SendEmailVerification;

public sealed class SendEmailVerificationCommandValidator : AbstractValidator<SendEmailVerificationCommand>
{
    public SendEmailVerificationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
