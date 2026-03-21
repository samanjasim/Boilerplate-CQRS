using System.Net;
using System.Net.Mail;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Starter.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var smtpClient = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username) &&
                !string.IsNullOrWhiteSpace(_settings.Password))
            {
                smtpClient.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = message.IsHtml
            };

            mailMessage.To.Add(message.To);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Recipient}", message.To);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", message.To);
            return false;
        }
    }
}
