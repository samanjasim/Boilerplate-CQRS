using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetAvailableProviders;

internal sealed class GetAvailableProvidersQueryHandler
    : IRequestHandler<GetAvailableProvidersQuery, Result<List<AvailableProviderDto>>>
{
    public Task<Result<List<AvailableProviderDto>>> Handle(
        GetAvailableProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var providers = new List<AvailableProviderDto>
        {
            // Email providers
            new(NotificationChannel.Email, ChannelProvider.Smtp, "SMTP",
                ["Host", "Port", "Username", "Password", "UseSsl", "SenderEmail", "SenderName"]),
            new(NotificationChannel.Email, ChannelProvider.SendGrid, "SendGrid",
                ["ApiKey", "SenderEmail", "SenderName"]),
            new(NotificationChannel.Email, ChannelProvider.Ses, "Amazon SES",
                ["AccessKeyId", "SecretAccessKey", "Region", "SenderEmail", "SenderName"]),

            // SMS providers
            new(NotificationChannel.Sms, ChannelProvider.Twilio, "Twilio SMS",
                ["AccountSid", "AuthToken", "FromNumber"]),

            // Push providers
            new(NotificationChannel.Push, ChannelProvider.Fcm, "Firebase Cloud Messaging",
                ["ProjectId", "ServiceAccountJson"]),
            new(NotificationChannel.Push, ChannelProvider.Apns, "Apple Push Notifications",
                ["TeamId", "KeyId", "PrivateKey", "BundleId"]),

            // WhatsApp providers
            new(NotificationChannel.WhatsApp, ChannelProvider.TwilioWhatsApp, "Twilio WhatsApp",
                ["AccountSid", "AuthToken", "FromNumber"]),
            new(NotificationChannel.WhatsApp, ChannelProvider.MetaWhatsApp, "Meta WhatsApp Business",
                ["AccessToken", "PhoneNumberId", "BusinessAccountId"]),

            // In-App (platform-managed, shown for reference)
            new(NotificationChannel.InApp, ChannelProvider.Ably, "Ably (Platform Managed)",
                []),
        };

        return Task.FromResult(Result.Success(providers));
    }
}
