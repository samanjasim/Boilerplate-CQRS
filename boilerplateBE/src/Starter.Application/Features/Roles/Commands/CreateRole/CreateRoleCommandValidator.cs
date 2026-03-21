using Starter.Domain.Identity.Entities;
using FluentValidation;

namespace Starter.Application.Features.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(Role.MaxNameLength)
            .WithMessage($"Role name must not exceed {Role.MaxNameLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(Role.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {Role.MaxDescriptionLength} characters.")
            .When(x => x.Description is not null);
    }
}
