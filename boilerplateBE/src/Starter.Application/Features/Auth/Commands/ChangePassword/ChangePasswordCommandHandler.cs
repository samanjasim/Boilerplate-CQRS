using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordService passwordService,
    ICurrentUserService currentUserService) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized());

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(currentUserService.UserId.Value));

        var passwordValid = await passwordService.VerifyPasswordAsync(request.CurrentPassword, user.PasswordHash);
        if (!passwordValid)
            return Result.Failure(UserErrors.InvalidCurrentPassword());

        var newPasswordHash = await passwordService.HashPasswordAsync(request.NewPassword);
        user.ChangePassword(newPasswordHash);
        user.RevokeRefreshToken();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
