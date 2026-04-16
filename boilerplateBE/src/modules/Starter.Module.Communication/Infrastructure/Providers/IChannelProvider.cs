using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

public interface IChannelProvider
{
    NotificationChannel Channel { get; }
    ChannelProvider ProviderType { get; }
    Task<ProviderDeliveryResult> SendAsync(ChannelDeliveryRequest request, CancellationToken ct = default);
}

public sealed record ChannelDeliveryRequest(
    string RecipientAddress,
    string? Subject,
    string Body,
    Dictionary<string, string> ProviderCredentials,
    Dictionary<string, string>? Metadata = null);

public sealed record ProviderDeliveryResult(
    bool Success,
    string? ProviderMessageId,
    string? ErrorMessage,
    int DurationMs);
