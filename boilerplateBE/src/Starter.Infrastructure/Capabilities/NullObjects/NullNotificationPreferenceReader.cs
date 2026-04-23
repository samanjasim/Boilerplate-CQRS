using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="INotificationPreferenceReader"/>.
/// Returns <c>true</c> (email enabled) as the default. The core
/// <see cref="Capabilities.NotificationPreferenceReaderService"/> is always
/// registered in the host, so this fallback is only used in isolated module
/// test setups that wire DI without <c>Starter.Infrastructure</c>.
/// </summary>
public sealed class NullNotificationPreferenceReader(
    ILogger<NullNotificationPreferenceReader> logger) : INotificationPreferenceReader
{
    public Task<bool> IsEmailEnabledAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Notification preference check skipped — returning default enabled (user: {UserId}, type: {Type})",
            userId, notificationType);
        return Task.FromResult(true);
    }
}
