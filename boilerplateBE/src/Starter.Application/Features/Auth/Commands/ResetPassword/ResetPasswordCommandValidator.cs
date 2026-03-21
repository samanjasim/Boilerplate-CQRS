using Starter.Application.Common.Validators;
using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Reset code is required.")
            .Length(6).WithMessage("Reset code must be 6 characters.");

        RuleFor(x => x.NewPassword)
            .ApplyPasswordRules("New password");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
