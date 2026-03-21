using FluentValidation;

namespace Starter.Application.Features.Roles.Commands.UpdateRolePermissions;

public sealed class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required.");

        RuleFor(x => x.PermissionIds)
            .NotNull().WithMessage("Permission IDs are required.");
    }
}
