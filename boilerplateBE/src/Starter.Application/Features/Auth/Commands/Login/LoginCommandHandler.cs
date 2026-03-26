using System.Text.Json;
using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Application.Features.Auth.DTOs;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    IApplicationDbContext context,
    IPasswordService passwordService,
    ITokenService tokenService,
    ITotpService totpService,
    IAuditContextProvider auditContext) : IRequestHandler<LoginCommand, Result<LoginResponseDto>>
{
    public async Task<Result<LoginResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = Email.Normalize(request.Email);
        var ipAddress = auditContext.IpAddress;
        var userAgent = auditContext.UserAgent;

        var user = await context.Users
            .WithRolesAndPermissions()
            .FirstOrDefaultAsync(u => u.Email.Value == normalizedEmail, cancellationToken);

        if (user is null)
        {
            RecordLoginHistory(request.Email, null, false, "InvalidCredentials", ipAddress, userAgent);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(UserErrors.InvalidCredentials());
        }

        if (user.IsLockedOut())
        {
            RecordLoginHistory(request.Email, user.Id, false, "AccountLocked", ipAddress, userAgent);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(UserErrors.AccountLocked());
        }

        if (!user.Status.CanLogin)
        {
            RecordLoginHistory(request.Email, user.Id, false, "AccountNotActive", ipAddress, userAgent);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(UserErrors.AccountNotActive());
        }

        var passwordValid = await passwordService.VerifyPasswordAsync(request.Password, user.PasswordHash);
        if (!passwordValid)
        {
            user.RecordFailedLogin();
            RecordLoginHistory(request.Email, user.Id, false, "InvalidCredentials", ipAddress, userAgent);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(UserErrors.InvalidCredentials());
        }

        // Check if 2FA is enabled
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                // Signal that 2FA code is required
                return Result.Success(new LoginResponseDto(null, null, null, null, RequiresTwoFactor: true));
            }

            // Validate TOTP code
            if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
                return Result.Failure<LoginResponseDto>(UserErrors.InvalidTwoFactorCode());

            var isValidTotp = totpService.ValidateCode(user.TwoFactorSecret, request.TwoFactorCode);
            var isValidBackup = false;

            // Check backup codes if TOTP fails
            if (!isValidTotp && !string.IsNullOrWhiteSpace(user.TwoFactorBackupCodes))
            {
                var hashedCodes = JsonSerializer.Deserialize<List<string>>(user.TwoFactorBackupCodes) ?? [];
                var hashedInput = totpService.HashBackupCode(request.TwoFactorCode);
                var matchIndex = hashedCodes.IndexOf(hashedInput);

                if (matchIndex >= 0)
                {
                    isValidBackup = true;
                    // Remove used backup code
                    hashedCodes.RemoveAt(matchIndex);
                    user.UpdateTwoFactorBackupCodes(JsonSerializer.Serialize(hashedCodes));
                }
            }

            if (!isValidTotp && !isValidBackup)
            {
                user.RecordFailedLogin();
                RecordLoginHistory(request.Email, user.Id, false, "2FAFailed", ipAddress, userAgent);
                await context.SaveChangesAsync(cancellationToken);
                return Result.Failure<LoginResponseDto>(UserErrors.InvalidTwoFactorCode());
            }
        }

        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .ToList();

        var permissions = user.GetPermissions().ToList();

        var tokenResult = await tokenService.GenerateTokensAsync(
            user.Id, user.Email.Value, roles, permissions, tenantId: user.TenantId);

        if (tokenResult.IsFailure)
            return Result.Failure<LoginResponseDto>(tokenResult.Error);

        var tokens = tokenResult.Value;

        user.SetRefreshToken(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        user.RecordSuccessfulLogin();

        // Create session
        var session = Session.Create(user.Id, tokens.RefreshToken, ipAddress, userAgent);
        context.Sessions.Add(session);

        // Record successful login
        RecordLoginHistory(request.Email, user.Id, true, null, ipAddress, userAgent);

        await context.SaveChangesAsync(cancellationToken);

        string? tenantSlug = null;
        string? tenantName = null;
        if (user.TenantId.HasValue)
        {
            var tenant = await context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == user.TenantId.Value)
                .Select(t => new { t.Slug, t.Name })
                .FirstOrDefaultAsync(cancellationToken);
            tenantSlug = tenant?.Slug;
            tenantName = tenant?.Name;
        }

        var userDto = user.ToDto(tenantSlug: tenantSlug, tenantName: tenantName);

        return Result.Success(new LoginResponseDto(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            userDto));
    }

    private void RecordLoginHistory(
        string email,
        Guid? userId,
        bool success,
        string? failureReason,
        string? ipAddress,
        string? userAgent)
    {
        var deviceInfo = DeviceInfoParser.Parse(userAgent);
        var loginHistory = LoginHistory.Create(email, userId, success, failureReason, ipAddress, userAgent, deviceInfo);
        context.LoginHistory.Add(loginHistory);
    }

}
