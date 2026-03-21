using Starter.Application.Common.Validators;
using Starter.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.AcceptInvite;

public sealed class AcceptInviteCommandValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation token is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(FullName.MaxFirstNameLength)
            .WithMessage($"First name must not exceed {FullName.MaxFirstNameLength} characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(FullName.MaxLastNameLength)
            .WithMessage($"Last name must not exceed {FullName.MaxLastNameLength} characters.");

        RuleFor(x => x.Password).ApplyPasswordRules();

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
