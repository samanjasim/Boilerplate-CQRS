using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.ForgotPassword;

internal sealed class ForgotPasswordCommandHandler(
    IApplicationDbContext context,
    IOtpService otpService,
    IEmailService emailService,
    IEmailTemplateService emailTemplateService) : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email.Value == Email.Normalize(request.Email), cancellationToken);

        // Security: Always return success to prevent user enumeration.
        // Attackers cannot determine whether an email exists in the system.
        if (user is null)
            return Result.Success();

        var otpCode = await otpService.GenerateAsync(OtpPurpose.PasswordReset, Email.Normalize(request.Email), cancellationToken);
        var emailMessage = emailTemplateService.RenderPasswordReset(user.Email.Value, user.FullName.GetFullName(), otpCode);
        await emailService.SendAsync(emailMessage, cancellationToken);

        return Result.Success();
    }
}
