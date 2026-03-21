using System.Text.Json;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.Disable2FA;

internal sealed class Disable2FACommandHandler(
    IApplicationDbContext context,
    ITotpService totpService,
    ICurrentUserService currentUserService) : IRequestHandler<Disable2FACommand, Result>
{
    public async Task<Result> Handle(Disable2FACommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized());

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(currentUserService.UserId.Value));

        if (!user.TwoFactorEnabled)
            return Result.Failure(UserErrors.TwoFactorNotEnabled());

        // Validate TOTP code or backup code
        var isValidTotp = totpService.ValidateCode(user.TwoFactorSecret!, request.Code);
        var isValidBackup = false;

        if (!isValidTotp && !string.IsNullOrWhiteSpace(user.TwoFactorBackupCodes))
        {
            var hashedCodes = JsonSerializer.Deserialize<List<string>>(user.TwoFactorBackupCodes) ?? [];
            var hashedInput = totpService.HashBackupCode(request.Code);
            isValidBackup = hashedCodes.Contains(hashedInput);
        }

        if (!isValidTotp && !isValidBackup)
            return Result.Failure(UserErrors.InvalidTwoFactorCode());

        user.DisableTwoFactor();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
