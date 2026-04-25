using Starter.Application.Common.Constants;
using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.ForgotPassword;

internal sealed class ForgotPasswordCommandHandler(
    IApplicationDbContext context,
    IOtpService otpService,
    IEmailTemplateService emailTemplateService,
    IIntegrationEventCollector eventCollector) : IRequestHandler<ForgotPasswordCommand, Result>
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
        eventCollector.Schedule(new SendEmailRequestedEvent(emailMessage, DateTime.UtcNow));

        // No domain state to write here; SaveChangesAsync is called solely to flush
        // the scheduled event into the ApplicationDbContext outbox. The interceptor
        // writes a single OutboxMessage row in one round trip — equivalent cost to
        // the inline SMTP call we replaced, with full retry + DLQ coverage added.
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
