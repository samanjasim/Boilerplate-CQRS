using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.AcceptInvite;

internal sealed class AcceptInviteCommandHandler(
    IApplicationDbContext context,
    IPasswordService passwordService) : IRequestHandler<AcceptInviteCommand, Result>
{
    public async Task<Result> Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
    {
        // Find invitation by token (IgnoreQueryFilters — anonymous endpoint)
        var invitation = await context.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == request.Token, cancellationToken);

        if (invitation is null)
            return Result.Failure(InvitationErrors.NotFoundByToken());

        if (invitation.IsAccepted)
            return Result.Failure(InvitationErrors.AlreadyAccepted());

        if (invitation.IsExpired())
            return Result.Failure(InvitationErrors.Expired());

        // Check email not already registered (global check)
        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == invitation.Email, cancellationToken);

        if (emailExists)
            return Result.Failure(UserErrors.EmailAlreadyExists(invitation.Email));

        // Find the role
        var role = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == invitation.RoleId, cancellationToken);

        if (role is null)
            return Result.Failure(InvitationErrors.RoleNotFound(invitation.RoleId));

        // Hash password and create user
        var email = Email.Create(invitation.Email);
        var fullName = FullName.Create(request.FirstName, request.LastName);
        var passwordHash = await passwordService.HashPasswordAsync(request.Password);

        var user = User.Create(
            invitation.Email, // use email as username
            email,
            fullName,
            passwordHash,
            tenantId: invitation.TenantId);

        // Assign the role from invitation
        user.AddRole(role);

        // Set EmailConfirmed and Active (they clicked the email link)
        user.ConfirmEmail();

        // Mark invitation as accepted
        invitation.Accept();

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
