using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Auth.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    IApplicationDbContext context,
    IPasswordService passwordService,
    ITokenService tokenService) : IRequestHandler<LoginCommand, Result<LoginResponseDto>>
{
    public async Task<Result<LoginResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .WithRolesAndPermissions()
            .FirstOrDefaultAsync(u => u.Email.Value == Email.Normalize(request.Email), cancellationToken);

        if (user is null)
            return Result.Failure<LoginResponseDto>(UserErrors.InvalidCredentials());

        if (user.IsLockedOut())
            return Result.Failure<LoginResponseDto>(UserErrors.AccountLocked());

        if (!user.Status.CanLogin)
            return Result.Failure<LoginResponseDto>(UserErrors.AccountNotActive());

        var passwordValid = await passwordService.VerifyPasswordAsync(request.Password, user.PasswordHash);
        if (!passwordValid)
        {
            user.RecordFailedLogin();
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(UserErrors.InvalidCredentials());
        }

        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .ToList();

        var permissions = user.GetPermissions().ToList();

        var tokenResult = await tokenService.GenerateTokensAsync(
            user.Id, user.Email.Value, roles, permissions, tenantId: null);

        if (tokenResult.IsFailure)
            return Result.Failure<LoginResponseDto>(tokenResult.Error);

        var tokens = tokenResult.Value;

        user.SetRefreshToken(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        user.RecordSuccessfulLogin();

        await context.SaveChangesAsync(cancellationToken);

        var userDto = user.ToDto();

        return Result.Success(new LoginResponseDto(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            userDto));
    }
}
