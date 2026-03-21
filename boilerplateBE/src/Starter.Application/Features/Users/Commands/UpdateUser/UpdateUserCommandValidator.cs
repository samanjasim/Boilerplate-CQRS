using Starter.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Starter.Application.Features.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(FullName.MaxFirstNameLength)
            .WithMessage($"First name must not exceed {FullName.MaxFirstNameLength} characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(FullName.MaxLastNameLength)
            .WithMessage($"Last name must not exceed {FullName.MaxLastNameLength} characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(Email.MaxLength)
            .WithMessage($"Email must not exceed {Email.MaxLength} characters.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{6,14}$")
            .WithMessage("Phone number must be in E.164 format (e.g., +9647701234567).")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
