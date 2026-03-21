using Starter.Application.Common.Validators;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.Register;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(User.MaxUsernameLength)
            .WithMessage($"Username must not exceed {User.MaxUsernameLength} characters.")
            .Matches(@"^[a-zA-Z0-9_]+$")
            .WithMessage("Username can only contain letters, numbers, and underscores.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(Email.MaxLength)
            .WithMessage($"Email must not exceed {Email.MaxLength} characters.");

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
