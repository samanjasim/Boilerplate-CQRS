using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

internal sealed class InviteUserCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IEmailService emailService,
    IEmailTemplateService emailTemplateService,
    IConfiguration configuration) : IRequestHandler<InviteUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        // Must belong to a tenant
        var tenantId = currentUserService.TenantId;
        if (tenantId is null)
            return Result.Failure<Guid>(InvitationErrors.TenantRequired());

        var inviterId = currentUserService.UserId!.Value;

        var normalizedEmail = Email.Normalize(request.Email);

        // Check email not already registered (global check)
        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists(request.Email));

        // Check no pending invitation for this email in this tenant
        var pendingExists = await context.Invitations
            .AnyAsync(i =>
                i.Email == normalizedEmail &&
                i.TenantId == tenantId.Value &&
                !i.IsAccepted &&
                i.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (pendingExists)
            return Result.Failure<Guid>(InvitationErrors.EmailAlreadyInvited(request.Email));

        // Verify the role exists and is accessible
        var role = await context.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
            return Result.Failure<Guid>(InvitationErrors.RoleNotFound(request.RoleId));

        // Create the invitation
        var invitation = Invitation.Create(
            normalizedEmail,
            role.Id,
            tenantId.Value,
            inviterId);

        context.Invitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        // Build accept URL
        var frontendUrl = configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
        var acceptUrl = $"{frontendUrl}/accept-invite?token={invitation.Token}";

        // Get inviter and tenant names for the email
        var inviter = await context.Users
            .FirstOrDefaultAsync(u => u.Id == inviterId, cancellationToken);

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        var inviterName = inviter?.FullName.GetFullName() ?? "A team member";
        var tenantName = tenant?.Name ?? "the organization";

        var emailMessage = emailTemplateService.RenderInvitation(
            normalizedEmail,
            inviterName,
            tenantName,
            role.Name,
            acceptUrl);

        await emailService.SendAsync(emailMessage, cancellationToken);

        return Result.Success(invitation.Id);
    }
}
