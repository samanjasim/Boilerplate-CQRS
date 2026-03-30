using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Settings.Commands.SetGlobalDefaultRole;

internal sealed class SetGlobalDefaultRoleCommandHandler(
    IApplicationDbContext context) : IRequestHandler<SetGlobalDefaultRoleCommand, Result>
{
    private const string SettingKey = "registration.default_role_id";

    public async Task<Result> Handle(SetGlobalDefaultRoleCommand request, CancellationToken cancellationToken)
    {
        // Validate the role exists if provided
        if (request.RoleId is not null)
        {
            var roleExists = await context.Roles
                .IgnoreQueryFilters()
                .AnyAsync(r => r.Id == request.RoleId.Value && r.IsActive, cancellationToken);

            if (!roleExists)
                return Result.Failure(InvitationErrors.RoleNotFound(request.RoleId.Value));
        }

        // Find or create the setting
        var setting = await context.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == null && s.Key == SettingKey, cancellationToken);

        if (setting is not null)
        {
            setting.UpdateValue(request.RoleId?.ToString() ?? "");
        }
        else
        {
            var newSetting = SystemSetting.Create(
                SettingKey,
                request.RoleId?.ToString() ?? "",
                tenantId: null,
                description: "Default role ID for new user registrations",
                category: "Registration",
                isSecret: false,
                dataType: "text");
            context.SystemSettings.Add(newSetting);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
