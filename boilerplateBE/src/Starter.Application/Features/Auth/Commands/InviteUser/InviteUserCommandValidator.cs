using Starter.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(Email.MaxLength)
            .WithMessage($"Email must not exceed {Email.MaxLength} characters.");
    }
}
