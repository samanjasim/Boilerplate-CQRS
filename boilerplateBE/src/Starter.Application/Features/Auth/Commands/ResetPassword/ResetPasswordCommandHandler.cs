using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IApplicationDbContext context,
    IOtpService otpService,
    IPasswordService passwordService) : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email.Value == Email.Normalize(request.Email), cancellationToken);

        if (user is null)
            return Result.Failure(Error.Validation("ResetPassword.InvalidCode", "Invalid or expired reset code."));

        var isValid = await otpService.ValidateAsync(OtpPurpose.PasswordReset, Email.Normalize(request.Email), request.Code, cancellationToken);
        if (!isValid)
            return Result.Failure(Error.Validation("ResetPassword.InvalidCode", "Invalid or expired reset code."));

        var newPasswordHash = await passwordService.HashPasswordAsync(request.NewPassword);
        user.ChangePassword(newPasswordHash);
        user.RevokeRefreshToken();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
