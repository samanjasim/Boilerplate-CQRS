using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.VerifyEmail;

internal sealed class VerifyEmailCommandHandler(
    IApplicationDbContext context,
    IOtpService otpService) : IRequestHandler<VerifyEmailCommand, Result>
{
    public async Task<Result> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email.Value == Email.Normalize(request.Email), cancellationToken);

        if (user is null)
            return Result.Failure(Error.Validation("VerifyEmail.InvalidCode", "Invalid or expired verification code."));

        var isValid = await otpService.ValidateAsync(OtpPurpose.EmailVerification, user.Email.Value, request.Code, cancellationToken);
        if (!isValid)
            return Result.Failure(Error.Validation("VerifyEmail.InvalidCode", "Invalid or expired verification code."));

        user.ConfirmEmail();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
