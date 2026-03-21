using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;

namespace Starter.Infrastructure.Email.Templates;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private const int OtpExpirationMinutes = 10;
    public EmailMessage RenderEmailVerification(
        string recipientEmail,
        string recipientName,
        string otpCode)
    {
        var body = WrapInLayout("Verify Your Email Address", $@"
            <h1 style=""margin:0 0 24px;font-size:24px;font-weight:600;color:#1a1a1a;"">
                Verify Your Email Address
            </h1>
            <p style=""margin:0 0 16px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                Hi {Escape(recipientName)},
            </p>
            <p style=""margin:0 0 24px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                Please use the following verification code to confirm your email address:
            </p>
            <div style=""margin:0 0 24px;padding:20px;background-color:#f0f4ff;border-radius:8px;text-align:center;"">
                <span style=""font-size:32px;font-weight:700;letter-spacing:6px;color:#2563eb;"">
                    {Escape(otpCode)}
                </span>
            </div>
            <p style=""margin:0 0 8px;font-size:14px;color:#6b7280;line-height:1.5;"">
                This code will expire in <strong>{OtpExpirationMinutes} minutes</strong>.
            </p>
            <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.5;"">
                If you did not request this verification, please ignore this email.
            </p>");

        return new EmailMessage(recipientEmail, "Verify Your Email Address", body);
    }

    public EmailMessage RenderPasswordReset(
        string recipientEmail,
        string recipientName,
        string otpCode)
    {
        var body = WrapInLayout("Reset Your Password", $@"
            <h1 style=""margin:0 0 24px;font-size:24px;font-weight:600;color:#1a1a1a;"">
                Reset Your Password
            </h1>
            <p style=""margin:0 0 16px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                Hi {Escape(recipientName)},
            </p>
            <p style=""margin:0 0 24px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                We received a request to reset your password. Use the following code to proceed:
            </p>
            <div style=""margin:0 0 24px;padding:20px;background-color:#fef3f2;border-radius:8px;text-align:center;"">
                <span style=""font-size:32px;font-weight:700;letter-spacing:6px;color:#dc2626;"">
                    {Escape(otpCode)}
                </span>
            </div>
            <p style=""margin:0 0 8px;font-size:14px;color:#6b7280;line-height:1.5;"">
                This code will expire in <strong>{OtpExpirationMinutes} minutes</strong>.
            </p>
            <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.5;"">
                If you did not request a password reset, please ignore this email.
                Your password will remain unchanged.
            </p>");

        return new EmailMessage(recipientEmail, "Reset Your Password", body);
    }

    public EmailMessage RenderInvitation(
        string recipientEmail,
        string inviterName,
        string tenantName,
        string roleName,
        string acceptUrl)
    {
        var body = WrapInLayout("You've Been Invited!", $@"
            <h1 style=""margin:0 0 24px;font-size:24px;font-weight:600;color:#1a1a1a;"">
                You've Been Invited!
            </h1>
            <p style=""margin:0 0 16px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                Hi there,
            </p>
            <p style=""margin:0 0 16px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                <strong>{Escape(inviterName)}</strong> has invited you to join
                <strong>{Escape(tenantName)}</strong> as a <strong>{Escape(roleName)}</strong>.
            </p>
            <p style=""margin:0 0 24px;font-size:16px;color:#4a4a4a;line-height:1.5;"">
                Click the button below to accept the invitation and set up your account:
            </p>
            <div style=""margin:0 0 24px;text-align:center;"">
                <a href=""{Escape(acceptUrl)}"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;font-size:16px;font-weight:600;border-radius:8px;"">
                    Accept Invitation
                </a>
            </div>
            <p style=""margin:0 0 8px;font-size:14px;color:#6b7280;line-height:1.5;"">
                This invitation will expire in <strong>7 days</strong>.
            </p>
            <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.5;"">
                If you did not expect this invitation, please ignore this email.
            </p>");

        return new EmailMessage(recipientEmail, $"You've been invited to join {tenantName}", body);
    }

    private static string WrapInLayout(string title, string content)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{Escape(title)}</title>
</head>
<body style=""margin:0;padding:0;background-color:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background-color:#f3f4f6;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""560"" cellspacing=""0"" cellpadding=""0"" style=""background-color:#ffffff;border-radius:12px;box-shadow:0 1px 3px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding:40px;"">
                            {content}
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:0 40px 32px;text-align:center;"">
                            <hr style=""border:none;border-top:1px solid #e5e7eb;margin:0 0 20px;"">
                            <p style=""margin:0;font-size:12px;color:#9ca3af;"">
                                &copy; {DateTime.UtcNow.Year} Starter. All rights reserved.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string Escape(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }
}
