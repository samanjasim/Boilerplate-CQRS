using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

/// <summary>
/// In-App notification provider. Platform-managed via Ably.
/// For now, creates an in-app delivery log entry. Full Ably push will be
/// wired in when the Enhanced Ably module is built.
/// </summary>
internal sealed class InAppProvider(ILogger<InAppProvider> logger) : IChannelProvider
{
    public NotificationChannel Channel => NotificationChannel.InApp;
    public Domain.Enums.ChannelProvider ProviderType => Domain.Enums.ChannelProvider.Ably;

    public Task<ProviderDeliveryResult> SendAsync(ChannelDeliveryRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // In-App is always "delivered" — it's recorded in the delivery log
        // and the frontend reads delivery logs for the current user as in-app notifications.
        // Full real-time push via Ably will be added with the Enhanced Ably module.
        logger.LogDebug("In-App notification recorded for {Recipient}", request.RecipientAddress);

        sw.Stop();
        return Task.FromResult(new ProviderDeliveryResult(
            true, $"inapp_{Guid.NewGuid():N}", null, (int)sw.ElapsedMilliseconds));
    }
}
