using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Auth.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    ITokenService tokenService) : IRequestHandler<RefreshTokenCommand, Result<LoginResponseDto>>
{
    public async Task<Result<LoginResponseDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .WithRolesAndPermissions()
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, cancellationToken);

        if (user is null)
            return Result.Failure<LoginResponseDto>(UserErrors.InvalidRefreshToken());

        if (!user.ValidateRefreshToken(request.RefreshToken))
        {
            user.RevokeRefreshToken();
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure<LoginResponseDto>(UserErrors.InvalidRefreshToken());
        }

        if (!user.Status.CanLogin)
            return Result.Failure<LoginResponseDto>(UserErrors.AccountNotActive());

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

        await context.SaveChangesAsync(cancellationToken);

        var userDto = user.ToDto();

        return Result.Success(new LoginResponseDto(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            userDto));
    }
}
