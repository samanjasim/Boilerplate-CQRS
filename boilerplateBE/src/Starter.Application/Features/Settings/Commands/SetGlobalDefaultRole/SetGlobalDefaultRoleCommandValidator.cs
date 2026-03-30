using FluentValidation;

namespace Starter.Application.Features.Settings.Commands.SetGlobalDefaultRole;

public sealed class SetGlobalDefaultRoleCommandValidator : AbstractValidator<SetGlobalDefaultRoleCommand>
{
    public SetGlobalDefaultRoleCommandValidator()
    {
        // RoleId is optional — null clears the global default
    }
}
