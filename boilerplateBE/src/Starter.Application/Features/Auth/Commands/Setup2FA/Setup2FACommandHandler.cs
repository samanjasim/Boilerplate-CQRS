using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Auth.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Starter.Application.Features.Auth.Commands.Setup2FA;

internal sealed class Setup2FACommandHandler(
    IApplicationDbContext context,
    ITotpService totpService,
    ICurrentUserService currentUserService,
    IConfiguration configuration) : IRequestHandler<Setup2FACommand, Result<Setup2FAResponseDto>>
{
    public async Task<Result<Setup2FAResponseDto>> Handle(Setup2FACommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return Result.Failure<Setup2FAResponseDto>(Error.Unauthorized());

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure<Setup2FAResponseDto>(UserErrors.NotFound(currentUserService.UserId.Value));

        if (user.TwoFactorEnabled)
            return Result.Failure<Setup2FAResponseDto>(UserErrors.TwoFactorAlreadyEnabled());

        var secret = totpService.GenerateSecret();
        var appName = configuration["AppSettings:AppName"] ?? "Starter";
        var qrCodeUri = totpService.GetQrCodeUri(user.Email.Value, secret, appName);

        return Result.Success(new Setup2FAResponseDto(secret, qrCodeUri));
    }
}
