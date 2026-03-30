using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using SharedRoles = Starter.Shared.Constants.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

internal sealed class InviteUserCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPermissionHierarchyService permissionHierarchyService,
    IEmailService emailService,
    IEmailTemplateService emailTemplateService,
    IConfiguration configuration) : IRequestHandler<InviteUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve tenant
        Guid? tenantId;
        if (currentUserService.TenantId is not null)
        {
            // Tenant users always invite into their own tenant
            tenantId = currentUserService.TenantId;
        }
        else
        {
            // Platform admin — use request.TenantId (null = platform invite)
            tenantId = request.TenantId;
        }

        // Validate tenant exists if specified
        if (tenantId is not null)
        {
            var tenantExists = await context.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == tenantId.Value, cancellationToken);

            if (!tenantExists)
                return Result.Failure<Guid>(InvitationErrors.TenantNotFound(tenantId.Value));
        }

        var inviterId = currentUserService.UserId!.Value;

        // 2. Validate email
        var normalizedEmail = Email.Normalize(request.Email);

        // Check email not already registered (global check)
        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists(request.Email));

        // Check no pending invitation for same email+tenantId scope
        var pendingExists = await context.Invitations
            .IgnoreQueryFilters()
            .AnyAsync(i =>
                i.Email == normalizedEmail &&
                i.TenantId == tenantId &&
                !i.IsAccepted &&
                i.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (pendingExists)
            return Result.Failure<Guid>(InvitationErrors.EmailAlreadyInvited(request.Email));

        // 3. Resolve role (chain: provided → tenant default → global default → "User")
        Guid roleId;

        if (request.RoleId is not null)
        {
            roleId = request.RoleId.Value;
        }
        else if (tenantId is not null)
        {
            var tenant = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

            if (tenant?.DefaultRegistrationRoleId is not null)
            {
                roleId = tenant.DefaultRegistrationRoleId.Value;
            }
            else
            {
                roleId = await ResolveGlobalDefaultRoleAsync(cancellationToken);
            }
        }
        else
        {
            roleId = await ResolveGlobalDefaultRoleAsync(cancellationToken);
        }

        // Verify the resolved role exists
        var role = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
            return Result.Failure<Guid>(InvitationErrors.RoleNotFound(roleId));

        // 4. Permission hierarchy check
        if (role.Name == SharedRoles.SuperAdmin && !currentUserService.IsInRole(SharedRoles.SuperAdmin))
            return Result.Failure<Guid>(InvitationErrors.SuperAdminOnly());

        if (!currentUserService.IsInRole(SharedRoles.SuperAdmin))
        {
            var canAssign = await permissionHierarchyService.CanAssignRoleAsync(roleId, cancellationToken);
            if (!canAssign)
                return Result.Failure<Guid>(InvitationErrors.PermissionEscalation());
        }

        // 5. Create invitation
        var invitation = Invitation.Create(
            normalizedEmail,
            role.Id,
            tenantId,
            inviterId);

        context.Invitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        // 6. Send email
        var frontendUrl = configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
        var acceptUrl = $"{frontendUrl}/accept-invite?token={invitation.Token}";

        var inviter = await context.Users
            .FirstOrDefaultAsync(u => u.Id == inviterId, cancellationToken);

        var inviterName = inviter?.FullName.GetFullName() ?? "A team member";

        string tenantName;
        if (tenantId is not null)
        {
            var tenantEntity = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
            tenantName = tenantEntity?.Name ?? "the organization";
        }
        else
        {
            tenantName = "the platform";
        }

        var emailMessage = emailTemplateService.RenderInvitation(
            normalizedEmail,
            inviterName,
            tenantName,
            role.Name,
            acceptUrl);

        await emailService.SendAsync(emailMessage, cancellationToken);

        return Result.Success(invitation.Id);
    }

    private async Task<Guid> ResolveGlobalDefaultRoleAsync(CancellationToken cancellationToken)
    {
        // Try system setting for default role
        var defaultRoleSetting = await context.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s =>
                s.Key == "registration.default_role_id" &&
                s.TenantId == null,
                cancellationToken);

        if (defaultRoleSetting is not null && Guid.TryParse(defaultRoleSetting.Value, out var settingRoleId))
            return settingRoleId;

        // Fallback: system "User" role
        var userRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstAsync(r => r.Name == SharedRoles.User && r.IsSystemRole, cancellationToken);

        return userRole.Id;
    }
}
