using System.Text.Json;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Auth.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.Verify2FA;

internal sealed class Verify2FACommandHandler(
    IApplicationDbContext context,
    ITotpService totpService,
    ICurrentUserService currentUserService) : IRequestHandler<Verify2FACommand, Result<Verify2FAResponseDto>>
{
    public async Task<Result<Verify2FAResponseDto>> Handle(Verify2FACommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return Result.Failure<Verify2FAResponseDto>(Error.Unauthorized());

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure<Verify2FAResponseDto>(UserErrors.NotFound(currentUserService.UserId.Value));

        if (user.TwoFactorEnabled)
            return Result.Failure<Verify2FAResponseDto>(UserErrors.TwoFactorAlreadyEnabled());

        if (!totpService.ValidateCode(request.Secret, request.Code))
            return Result.Failure<Verify2FAResponseDto>(UserErrors.InvalidTwoFactorCode());

        var backupCodes = totpService.GenerateBackupCodes();
        var hashedCodes = backupCodes.Select(c => totpService.HashBackupCode(c)).ToList();
        var hashedCodesJson = JsonSerializer.Serialize(hashedCodes);

        user.EnableTwoFactor(request.Secret, hashedCodesJson);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new Verify2FAResponseDto(backupCodes));
    }
}
