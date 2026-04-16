using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

internal sealed class SmtpEmailProvider(ILogger<SmtpEmailProvider> logger) : IChannelProvider
{
    public NotificationChannel Channel => NotificationChannel.Email;
    public Domain.Enums.ChannelProvider ProviderType => Domain.Enums.ChannelProvider.Smtp;

    public async Task<ProviderDeliveryResult> SendAsync(ChannelDeliveryRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var host = request.ProviderCredentials.GetValueOrDefault("Host", "localhost");
            var port = int.Parse(request.ProviderCredentials.GetValueOrDefault("Port", "1025"));
            var username = request.ProviderCredentials.GetValueOrDefault("Username", "");
            var password = request.ProviderCredentials.GetValueOrDefault("Password", "");
            var useSsl = bool.Parse(request.ProviderCredentials.GetValueOrDefault("UseSsl", "false"));
            var senderEmail = request.ProviderCredentials.GetValueOrDefault("SenderEmail", "noreply@localhost");
            var senderName = request.ProviderCredentials.GetValueOrDefault("SenderName", "System");

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30_000, // 30 seconds to prevent blocking MassTransit consumer
            };

            if (!string.IsNullOrWhiteSpace(username))
                client.Credentials = new NetworkCredential(username, password);

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = request.Subject ?? "",
                Body = request.Body,
                IsBodyHtml = IsHtmlContent(request.Body),
            };
            message.To.Add(request.RecipientAddress);

            await client.SendMailAsync(message, ct);
            sw.Stop();

            logger.LogInformation("SMTP email sent to {Recipient} via {Host}:{Port} in {Duration}ms",
                request.RecipientAddress, host, port, sw.ElapsedMilliseconds);

            return new ProviderDeliveryResult(true, null, null, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "SMTP email delivery failed to {Recipient}", request.RecipientAddress);
            return new ProviderDeliveryResult(false, null, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }

    private static bool IsHtmlContent(string body) =>
        body.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("<p>", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("<br", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("<table", StringComparison.OrdinalIgnoreCase);
}
