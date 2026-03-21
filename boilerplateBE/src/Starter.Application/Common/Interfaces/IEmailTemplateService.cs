using Starter.Application.Common.Models;

namespace Starter.Application.Common.Interfaces;

public interface IEmailTemplateService
{
    EmailMessage RenderEmailVerification(string recipientEmail, string recipientName, string otpCode);
    EmailMessage RenderPasswordReset(string recipientEmail, string recipientName, string otpCode);
}
