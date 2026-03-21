using Starter.Domain.Identity.Entities;
using FluentValidation;

namespace Starter.Application.Features.Roles.Commands.UpdateRole;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Role ID is required.");

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
