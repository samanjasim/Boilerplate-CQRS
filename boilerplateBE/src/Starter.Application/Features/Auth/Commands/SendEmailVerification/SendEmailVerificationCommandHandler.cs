using Starter.Application.Common.Constants;
using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.SendEmailVerification;

internal sealed class SendEmailVerificationCommandHandler(
    IApplicationDbContext context,
    IOtpService otpService,
    IEmailTemplateService emailTemplateService,
    IIntegrationEventCollector eventCollector) : IRequestHandler<SendEmailVerificationCommand, Result>
{
    public async Task<Result> Handle(SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email.Value == Email.Normalize(request.Email), cancellationToken);

        // Security: Always return success to prevent user enumeration.
        if (user is null)
            return Result.Success();

        if (user.EmailConfirmed)
            return Result.Failure(Error.Validation("SendEmailVerification.AlreadyVerified", "Email is already verified."));

        var otpCode = await otpService.GenerateAsync(OtpPurpose.EmailVerification, user.Email.Value, cancellationToken);
        var emailMessage = emailTemplateService.RenderEmailVerification(user.Email.Value, user.FullName.GetFullName(), otpCode);
        eventCollector.Schedule(new SendEmailRequestedEvent(emailMessage, DateTime.UtcNow));

        // No domain state to write; SaveChangesAsync flushes the scheduled event
        // into the outbox. See ForgotPasswordCommandHandler for the same pattern.
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
