using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using RoleConstants = Starter.Shared.Constants.Roles;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IPasswordService passwordService,
    IOtpService otpService,
    IEmailService emailService,
    IEmailTemplateService emailTemplateService,
    IFeatureFlagService flags,
    IUsageTracker usageTracker,
    ICurrentUserService currentUser) : IRequestHandler<RegisterUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            var maxUsers = await flags.GetValueAsync<int>("users.max_count", cancellationToken);
            var currentCount = await usageTracker.GetAsync(tenantId.Value, "users", cancellationToken);
            if (currentCount >= maxUsers)
                return Result.Failure<Guid>(FeatureFlagErrors.QuotaExceeded("users", maxUsers));
        }

        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == Email.Normalize(request.Email), cancellationToken);

        if (emailExists)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists(request.Email));

        var usernameExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username == request.Username.Trim(), cancellationToken);

        if (usernameExists)
            return Result.Failure<Guid>(UserErrors.UsernameAlreadyExists(request.Username));

        var email = Email.Create(request.Email);
        var fullName = FullName.Create(request.FirstName, request.LastName);
        var passwordHash = await passwordService.HashPasswordAsync(request.Password);

        var user = User.Create(request.Username.Trim(), email, fullName, passwordHash);

        var defaultRole = await ResolveDefaultRoleAsync(cancellationToken);
        if (defaultRole is not null)
            user.AddRole(defaultRole);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        if (tenantId.HasValue)
            await usageTracker.IncrementAsync(tenantId.Value, "users", ct: cancellationToken);

        var otpCode = await otpService.GenerateAsync(OtpPurpose.EmailVerification, user.Email.Value, cancellationToken);
        var emailMessage = emailTemplateService.RenderEmailVerification(user.Email.Value, user.FullName.GetFullName(), otpCode);
        await emailService.SendAsync(emailMessage, cancellationToken);

        return Result.Success(user.Id);
    }

    /// <summary>
    /// Resolves the default role for self-registration:
    /// 1. Global setting registration.default_role_id → 2. Fallback to system "User" role
    /// </summary>
    private async Task<Role?> ResolveDefaultRoleAsync(CancellationToken cancellationToken)
    {
        // Check global default role setting
        var defaultRoleSetting = await context.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s =>
                s.Key == "registration.default_role_id" &&
                s.TenantId == null,
                cancellationToken);

        if (defaultRoleSetting is not null && Guid.TryParse(defaultRoleSetting.Value, out var settingRoleId))
        {
            var role = await context.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == settingRoleId, cancellationToken);

            if (role is not null) return role;
        }

        // Fallback: system "User" role
        return await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == RoleConstants.User && r.IsSystemRole, cancellationToken);
    }
}
